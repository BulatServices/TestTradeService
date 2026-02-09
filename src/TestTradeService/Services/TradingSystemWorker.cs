using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Фоновый воркер, координирующий источники данных, конвейер и мониторинг.
/// </summary>
public sealed class TradingSystemWorker : BackgroundService, IRuntimeReconfigurationService
{
    private readonly IChannelFactory _channelFactory;
    private readonly IReadOnlyList<IMarketDataSource> _sources;
    private readonly IDataPipeline _pipeline;
    private readonly IStorage _storage;
    private readonly IMonitoringService _monitoring;
    private readonly IMarketDataEventBus _eventBus;
    private readonly ILogger<TradingSystemWorker> _logger;
    private readonly SemaphoreSlim _sourceLifecycleLock = new(1, 1);
    private readonly TimeSpan _pipelineDrainTimeout;

    private Channel<Tick>? _channel;
    private ChannelWriter<Tick>? _writer;
    private CancellationTokenSource? _sourcesCts;
    private List<Task> _sourceTasks = [];
    private CancellationToken _hostToken;
    private Task? _pipelineTask;
    private Task? _monitoringTask;
    private long _acceptedTickCount;
    private long _droppedTicksOnShutdown;

    /// <summary>
    /// Возвращает количество тиков, потерянных при остановке из-за таймаута дренажа конвейера.
    /// </summary>
    public long DroppedTicksOnShutdown => Interlocked.Read(ref _droppedTicksOnShutdown);

    /// <summary>
    /// Инициализирует воркер торговой системы.
    /// </summary>
    /// <param name="channelFactory">Фабрика каналов обмена сообщениями.</param>
    /// <param name="sources">Набор источников рыночных данных.</param>
    /// <param name="pipeline">Конвейер обработки тиков.</param>
    /// <param name="storage">Хранилище данных.</param>
    /// <param name="monitoring">Сервис мониторинга.</param>
    /// <param name="eventBus">Шина событий рыночного потока.</param>
    /// <param name="logger">Логгер воркера.</param>
    /// <param name="pipelineDrainTimeout">Максимальное время ожидания дренажа конвейера при остановке.</param>
    public TradingSystemWorker(
        IChannelFactory channelFactory,
        IEnumerable<IMarketDataSource> sources,
        IDataPipeline pipeline,
        IStorage storage,
        IMonitoringService monitoring,
        IMarketDataEventBus eventBus,
        ILogger<TradingSystemWorker> logger,
        TimeSpan? pipelineDrainTimeout = null)
    {
        _channelFactory = channelFactory;
        _pipeline = pipeline;
        _storage = storage;
        _monitoring = monitoring;
        _eventBus = eventBus;
        _logger = logger;
        _pipelineDrainTimeout = pipelineDrainTimeout ?? TimeSpan.FromSeconds(30);

        _sources = sources
            .OrderBy(s => s.Exchange)
            .ThenBy(s => s.Transport)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation("Market data sources: {Sources}", string.Join(", ", _sources.Select(s => s.Name)));
    }

    /// <summary>
    /// Запускает все подсистемы и ожидает остановки хоста.
    /// </summary>
    /// <param name="stoppingToken">Токен остановки hosted service.</param>
    /// <returns>Задача жизненного цикла воркера.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostToken = stoppingToken;
        _channel = _channelFactory.CreateTickChannel();
        _writer = _channel.Writer;
        Interlocked.Exchange(ref _acceptedTickCount, 0);
        Interlocked.Exchange(ref _droppedTicksOnShutdown, 0);

        await StartSourcesAsync(stoppingToken);

        _pipelineTask = _pipeline.StartAsync(_channel.Reader, stoppingToken);
        _monitoringTask = ReportMonitoringAsync(stoppingToken);

        try
        {
            await Task.WhenAll(_pipelineTask, _monitoringTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Перезапускает источники данных для применения обновленной конфигурации.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача применения конфигурации.</returns>
    public async Task ApplySourcesAsync(CancellationToken cancellationToken)
    {
        await _sourceLifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_writer is null)
            {
                return;
            }

            await StopSourcesCoreAsync(cancellationToken);
            await StartSourcesCoreAsync(_writer, _hostToken);
        }
        finally
        {
            _sourceLifecycleLock.Release();
        }
    }

    /// <summary>
    /// Запрашивает корректную остановку всех источников, затем останавливает воркер.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача остановки воркера.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _sourceLifecycleLock.WaitAsync(cancellationToken);
        try
        {
            await StopSourcesCoreAsync(cancellationToken);

            try
            {
                _writer?.Complete();
            }
            catch (ChannelClosedException)
            {
            }
        }
        finally
        {
            _sourceLifecycleLock.Release();
        }

        await DrainPipelineOnShutdownAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task StartSourcesAsync(CancellationToken cancellationToken)
    {
        await _sourceLifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_writer is null)
            {
                return;
            }

            await StartSourcesCoreAsync(_writer, cancellationToken);
        }
        finally
        {
            _sourceLifecycleLock.Release();
        }
    }

    private Task StartSourcesCoreAsync(ChannelWriter<Tick> writer, CancellationToken hostToken)
    {
        _sourcesCts = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
        var sourceToken = _sourcesCts.Token;
        var countingWriter = new CountingChannelWriter(writer, () => Interlocked.Increment(ref _acceptedTickCount));

        _sourceTasks = _sources
            .Select(source => Task.Run(() => RunSourceAsync(source, countingWriter, sourceToken), sourceToken))
            .ToList();

        return Task.CompletedTask;
    }

    private async Task StopSourcesCoreAsync(CancellationToken cancellationToken)
    {
        if (_sourcesCts is null)
        {
            return;
        }

        try
        {
            _sourcesCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        foreach (var source in _sources)
        {
            try
            {
                await source.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop source {Source}", source.Name);
            }
        }

        try
        {
            await Task.WhenAll(_sourceTasks);
        }
        catch (OperationCanceledException)
        {
        }

        _sourceTasks = [];
        _sourcesCts.Dispose();
        _sourcesCts = null;
    }

    private async Task RunSourceAsync(IMarketDataSource source, ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        try
        {
            await StoreSourceStatusWithHistoryAsync(new SourceStatus
            {
                Exchange = source.Exchange,
                Source = source.Name,
                IsOnline = true,
                LastUpdate = DateTimeOffset.UtcNow,
                Message = null
            }, cancellationToken);
            await source.StartAsync(writer, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Source {Source} stopped", source.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Source {Source} crashed", source.Name);
            await StoreSourceStatusWithHistoryAsync(new SourceStatus
            {
                Exchange = source.Exchange,
                Source = source.Name,
                IsOnline = false,
                LastUpdate = DateTimeOffset.UtcNow,
                Message = ex.Message
            }, cancellationToken);
        }
    }

    private async Task StoreSourceStatusWithHistoryAsync(SourceStatus status, CancellationToken cancellationToken)
    {
        await _storage.StoreSourceStatusAsync(status, cancellationToken);
        await _storage.StoreSourceStatusEventAsync(status, cancellationToken);
    }

    private async Task ReportMonitoringAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = _monitoring.Snapshot();
            _eventBus.PublishMonitoring(snapshot);

            foreach (var stats in snapshot.ExchangeStats.Values.OrderBy(s => s.Exchange))
            {
                _logger.LogInformation(
                    "Exchange {Exchange} ticks={Ticks} aggregates={Agg} avgDelayMs={Delay:F0}",
                    stats.Exchange,
                    stats.TickCount,
                    stats.AggregateCount,
                    stats.AverageDelayMs);
            }

            foreach (var stats in snapshot.SourceStats.Values)
            {
                _logger.LogInformation(
                    "Source {Source} ticks={Ticks} aggregates={Agg} avgDelayMs={Delay:F0}",
                    stats.Source,
                    stats.TickCount,
                    stats.AggregateCount,
                    stats.AverageDelayMs);
            }

            foreach (var warning in snapshot.Warnings)
            {
                _logger.LogWarning("Monitoring warning: {Warning}", warning);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private async Task DrainPipelineOnShutdownAsync(CancellationToken cancellationToken)
    {
        if (_pipelineTask is null)
        {
            return;
        }

        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCts.CancelAfter(_pipelineDrainTimeout);

        try
        {
            await _pipelineTask.WaitAsync(drainCts.Token);
            Interlocked.Exchange(ref _droppedTicksOnShutdown, 0);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var accepted = Interlocked.Read(ref _acceptedTickCount);
            var consumed = _pipeline.ConsumedTickCount;
            var dropped = Math.Max(0, accepted - consumed);
            Interlocked.Exchange(ref _droppedTicksOnShutdown, dropped);
            _logger.LogWarning(
                "Pipeline drain timeout reached after {Timeout}. accepted={Accepted} consumed={Consumed} dropped={Dropped}",
                _pipelineDrainTimeout,
                accepted,
                consumed,
                dropped);
        }
    }

    private sealed class CountingChannelWriter : ChannelWriter<Tick>
    {
        private readonly ChannelWriter<Tick> _inner;
        private readonly Action _onWrite;

        public CountingChannelWriter(ChannelWriter<Tick> inner, Action onWrite)
        {
            _inner = inner;
            _onWrite = onWrite;
        }

        public override bool TryComplete(Exception? error = null) => _inner.TryComplete(error);

        public override bool TryWrite(Tick item)
        {
            var written = _inner.TryWrite(item);
            if (written)
            {
                _onWrite();
            }

            return written;
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
            => _inner.WaitToWriteAsync(cancellationToken);

        public override async ValueTask WriteAsync(Tick item, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(item, cancellationToken);
            _onWrite();
        }
    }
}

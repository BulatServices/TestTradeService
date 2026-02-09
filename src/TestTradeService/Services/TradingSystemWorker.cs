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
    private readonly ChannelFactory _channelFactory;
    private readonly IReadOnlyList<IMarketDataSource> _sources;
    private readonly DataPipeline _pipeline;
    private readonly IStorage _storage;
    private readonly IMonitoringService _monitoring;
    private readonly IMarketDataEventBus _eventBus;
    private readonly ILogger<TradingSystemWorker> _logger;
    private readonly SemaphoreSlim _sourceLifecycleLock = new(1, 1);

    private Channel<Tick>? _channel;
    private ChannelWriter<Tick>? _writer;
    private CancellationTokenSource? _sourcesCts;
    private List<Task> _sourceTasks = [];
    private CancellationToken _hostToken;

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
    public TradingSystemWorker(
        ChannelFactory channelFactory,
        IEnumerable<IMarketDataSource> sources,
        DataPipeline pipeline,
        IStorage storage,
        IMonitoringService monitoring,
        IMarketDataEventBus eventBus,
        ILogger<TradingSystemWorker> logger)
    {
        _channelFactory = channelFactory;
        _pipeline = pipeline;
        _storage = storage;
        _monitoring = monitoring;
        _eventBus = eventBus;
        _logger = logger;

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

        await StartSourcesAsync(stoppingToken);

        var pipelineTask = _pipeline.StartAsync(_channel.Reader, stoppingToken);
        var monitoringTask = ReportMonitoringAsync(stoppingToken);

        try
        {
            await Task.WhenAll(pipelineTask, monitoringTask);
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
        }
        finally
        {
            _sourceLifecycleLock.Release();
        }

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

        _sourceTasks = _sources
            .Select(source => Task.Run(() => RunSourceAsync(source, writer, sourceToken), sourceToken))
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
}

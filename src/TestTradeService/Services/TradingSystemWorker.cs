using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Фоновый воркер, координирующий источники данных, конвейер и мониторинг.
/// </summary>
public sealed class TradingSystemWorker : BackgroundService
{
    private readonly ChannelFactory _channelFactory;
    private readonly IEnumerable<IMarketDataSource> _sources;
    private readonly DataPipeline _pipeline;
    private readonly IStorage _storage;
    private readonly IMonitoringService _monitoring;
    private readonly ILogger<TradingSystemWorker> _logger;

    /// <summary>
    /// Инициализирует воркер торговой системы.
    /// </summary>
    public TradingSystemWorker(
        ChannelFactory channelFactory,
        IEnumerable<IMarketDataSource> sources,
        DataPipeline pipeline,
        IStorage storage,
        IMonitoringService monitoring,
        ILogger<TradingSystemWorker> logger)
    {
        _channelFactory = channelFactory;
        _sources = sources;
        _pipeline = pipeline;
        _storage = storage;
        _monitoring = monitoring;
        _logger = logger;
    }

    /// <summary>
    /// Запускает все подсистемы и ожидает их завершения.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = _channelFactory.CreateTickChannel();
        var sourceTasks = _sources.Select(source => RunSourceAsync(source, channel.Writer, stoppingToken)).ToList();
        var pipelineTask = _pipeline.StartAsync(channel.Reader, stoppingToken);
        var monitoringTask = ReportMonitoringAsync(stoppingToken);

        await Task.WhenAll(sourceTasks.Append(pipelineTask).Append(monitoringTask));
    }

    /// <summary>
    /// Запрашивает корректную остановку всех источников, затем останавливает воркер.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
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

        await base.StopAsync(cancellationToken);
    }

    private async Task RunSourceAsync(IMarketDataSource source, ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.StoreSourceStatusAsync(new SourceStatus
            {
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
            await _storage.StoreSourceStatusAsync(new SourceStatus
            {
                Source = source.Name,
                IsOnline = false,
                LastUpdate = DateTimeOffset.UtcNow,
                Message = ex.Message
            }, cancellationToken);
        }
    }

    private async Task ReportMonitoringAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = _monitoring.Snapshot();
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

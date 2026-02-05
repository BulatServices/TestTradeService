using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Основной конвейер обработки рыночных данных.
/// </summary>
public sealed class DataPipeline
{
    private readonly TickNormalizer _normalizer = new();
    private readonly TickFilter _filter = new(new[] { "BTC-USD", "ETH-USD", "SOL-USD", "XRP-USD" });
    private readonly TickDeduplicator _deduplicator = new();
    private readonly IAggregationService _aggregationService;
    private readonly IStorage _storage;
    private readonly AlertingService _alerting;
    private readonly IMonitoringService _monitoring;
    private readonly ILogger<DataPipeline> _logger;

    /// <summary>
    /// Инициализирует конвейер обработки тиков.
    /// </summary>
    public DataPipeline(
        IAggregationService aggregationService,
        IStorage storage,
        AlertingService alerting,
        IMonitoringService monitoring,
        ILogger<DataPipeline> logger)
    {
        _aggregationService = aggregationService;
        _storage = storage;
        _alerting = alerting;
        _monitoring = monitoring;
        _logger = logger;
    }

    /// <summary>
    /// Запускает чтение тиков из канала и их последовательную обработку.
    /// </summary>
    public async Task StartAsync(ChannelReader<Tick> reader, CancellationToken cancellationToken)
    {
        await foreach (var tick in reader.ReadAllAsync(cancellationToken))
        {
            var normalized = _normalizer.Normalize(tick);
            if (!_filter.IsAllowed(normalized))
            {
                continue;
            }

            if (_deduplicator.IsDuplicate(normalized))
            {
                continue;
            }

            var delay = DateTimeOffset.UtcNow - normalized.Timestamp;
            _monitoring.RecordDelay(normalized.Source, delay);
            _monitoring.RecordTick(normalized.Source, normalized);

            await _storage.StoreTickAsync(normalized, cancellationToken);
            var metrics = _aggregationService.UpdateMetrics(normalized);
            var aggregates = _aggregationService.Update(normalized);

            foreach (var candle in aggregates)
            {
                _monitoring.RecordAggregate(normalized.Source, candle);
                await _storage.StoreAggregateAsync(candle, cancellationToken);
            }

            await _alerting.HandleAsync(normalized, metrics, cancellationToken);
        }

        _logger.LogInformation("Pipeline stopped");
    }
}

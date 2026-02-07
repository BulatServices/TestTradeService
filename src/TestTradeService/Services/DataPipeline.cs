using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Основной конвейер обработки рыночных данных.
/// </summary>
public sealed class DataPipeline
{
    private readonly TickNormalizer _normalizer = new();
    private readonly TickFilter _filter;
    private readonly TickDeduplicator _deduplicator = new();
    private readonly IAggregationService _aggregationService;
    private readonly IStorage _storage;
    private readonly AlertingService _alerting;
    private readonly IMonitoringService _monitoring;
    private readonly ILogger<DataPipeline> _logger;

    /// <summary>
    /// Инициализирует конвейер обработки тиков.
    /// </summary>
    /// <param name="aggregationService">Сервис агрегации тиков.</param>
    /// <param name="storage">Хранилище данных.</param>
    /// <param name="alerting">Сервис оповещений.</param>
    /// <param name="monitoring">Сервис мониторинга.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    /// <param name="logger">Логгер конвейера.</param>
    public DataPipeline(
        IAggregationService aggregationService,
        IStorage storage,
        AlertingService alerting,
        IMonitoringService monitoring,
        MarketInstrumentsConfig instrumentsConfig,
        ILogger<DataPipeline> logger)
    {
        _aggregationService = aggregationService;
        _storage = storage;
        _alerting = alerting;
        _monitoring = monitoring;
        _filter = new TickFilter(instrumentsConfig.GetAllSymbols());
        _logger = logger;
    }

    /// <summary>
    /// Запускает чтение тиков из канала и их последовательную обработку.
    /// </summary>
    /// <param name="reader">Канал для чтения тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения конвейера.</returns>
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

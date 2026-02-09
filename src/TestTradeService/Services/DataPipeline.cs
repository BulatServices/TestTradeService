using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Основной конвейер обработки рыночных данных.
/// </summary>
public sealed class DataPipeline : IDataPipeline
{
    private readonly TickNormalizer _normalizer = new();
    private readonly TickFilter _filter;
    private readonly TickDeduplicator _deduplicator = new();
    private readonly IAggregationService _aggregationService;
    private readonly IStorage _storage;
    private readonly IAlertingService _alerting;
    private readonly IMonitoringService _monitoring;
    private readonly IMarketDataEventBus _eventBus;
    private readonly ILogger<DataPipeline> _logger;
    private long _consumedTickCount;

    /// <summary>
    /// Возвращает количество тиков, считанных конвейером из входного канала.
    /// </summary>
    public long ConsumedTickCount => Interlocked.Read(ref _consumedTickCount);

    /// <summary>
    /// Инициализирует конвейер обработки тиков.
    /// </summary>
    /// <param name="aggregationService">Сервис агрегации тиков.</param>
    /// <param name="storage">Хранилище данных.</param>
    /// <param name="alerting">Сервис оповещений.</param>
    /// <param name="monitoring">Сервис мониторинга.</param>
    /// <param name="eventBus">Шина событий рыночного потока.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    /// <param name="logger">Логгер конвейера.</param>
    public DataPipeline(
        IAggregationService aggregationService,
        IStorage storage,
        IAlertingService alerting,
        IMonitoringService monitoring,
        IMarketDataEventBus eventBus,
        MarketInstrumentsConfig instrumentsConfig,
        ILogger<DataPipeline> logger)
    {
        _aggregationService = aggregationService;
        _storage = storage;
        _alerting = alerting;
        _monitoring = monitoring;
        _eventBus = eventBus;
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
            Interlocked.Increment(ref _consumedTickCount);
            await _storage.StoreRawTickAsync(BuildRawTick(tick), cancellationToken);

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
            _eventBus.PublishTick(normalized);

            await _storage.StoreTickAsync(normalized, cancellationToken);
            var metrics = _aggregationService.UpdateMetrics(normalized);
            var aggregates = _aggregationService.Update(normalized);

            foreach (var candle in aggregates)
            {
                _monitoring.RecordAggregate(normalized.Source, candle);
                await _storage.StoreAggregateAsync(candle, cancellationToken);
                _eventBus.PublishAggregate(candle);
            }

            var alerts = await _alerting.HandleAsync(normalized, metrics, cancellationToken);
            foreach (var alert in alerts)
            {
                _eventBus.PublishAlert(alert);
            }
        }

        _logger.LogInformation("Pipeline stopped");
    }

    private RawTick BuildRawTick(Tick tick)
    {
        return new RawTick
        {
            Exchange = string.IsNullOrWhiteSpace(tick.RawExchange) ? ExtractExchangeFromSource(tick.Source) : tick.RawExchange,
            Source = tick.Source,
            Symbol = tick.Symbol,
            MarketType = string.IsNullOrWhiteSpace(tick.RawMarketType) ? MarketType.Spot.ToString() : tick.RawMarketType,
            Price = tick.Price,
            Volume = tick.Volume,
            TradeId = tick.TradeId,
            EventTimestamp = tick.Timestamp,
            ReceivedAt = tick.RawReceivedAt ?? tick.Timestamp,
            Payload = BuildPayload(tick),
            Metadata = tick.RawMetadata
        };
    }

    private string BuildPayload(Tick tick)
    {
        if (!string.IsNullOrWhiteSpace(tick.RawPayload))
        {
            return tick.RawPayload;
        }

        try
        {
            return JsonSerializer.Serialize(new
            {
                source = tick.Source,
                symbol = tick.Symbol,
                price = tick.Price,
                volume = tick.Volume,
                timestamp = tick.Timestamp,
                tradeId = tick.TradeId
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to serialize fallback raw payload for source {Source}", tick.Source);
            return "{\"error\":\"payload_serialize_failed\"}";
        }
    }

    private static string ExtractExchangeFromSource(string source)
    {
        var parts = source.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0] : source;
    }
}

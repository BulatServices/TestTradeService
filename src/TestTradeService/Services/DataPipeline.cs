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
    private const string MissingPayload = "{\"error\":\"payload_missing\"}";

    private readonly TickNormalizer _normalizer = new();
    private readonly TickFilter _filter;
    private readonly TickDeduplicator _deduplicator = new();
    private readonly IAggregationService _aggregationService;
    private readonly IStorage _storage;
    private readonly IAlertingService _alerting;
    private readonly IMonitoringService _monitoring;
    private readonly IMarketDataEventBus _eventBus;
    private readonly ILogger<DataPipeline> _logger;
    private readonly PipelinePerformanceOptions _performanceOptions;
    private readonly SemaphoreSlim _alertingGate;
    private readonly object _aggregationSync = new();
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
    /// <param name="performanceOptions">Настройки производительности конвейера.</param>
    public DataPipeline(
        IAggregationService aggregationService,
        IStorage storage,
        IAlertingService alerting,
        IMonitoringService monitoring,
        IMarketDataEventBus eventBus,
        MarketInstrumentsConfig instrumentsConfig,
        ILogger<DataPipeline> logger,
        PipelinePerformanceOptions? performanceOptions = null)
    {
        _aggregationService = aggregationService;
        _storage = storage;
        _alerting = alerting;
        _monitoring = monitoring;
        _eventBus = eventBus;
        _filter = new TickFilter(instrumentsConfig.GetAllSymbols());
        _logger = logger;
        _performanceOptions = performanceOptions ?? new PipelinePerformanceOptions();
        _alertingGate = new SemaphoreSlim(Math.Max(1, _performanceOptions.AlertingConcurrency));
    }

    /// <summary>
    /// Запускает чтение тиков из канала и их обработку с партиционированием по символу.
    /// </summary>
    /// <param name="reader">Канал для чтения тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения конвейера.</returns>
    public async Task StartAsync(ChannelReader<Tick> reader, CancellationToken cancellationToken)
    {
        var partitionCount = Math.Max(1, _performanceOptions.PartitionCount);
        var channelCapacity = Math.Max(1, _performanceOptions.BatchSize) * Math.Max(1, _performanceOptions.MaxInMemoryBatches);
        var partitions = CreatePartitions(partitionCount, channelCapacity);

        await using var storageWriter = new BufferedStorageWriter(_storage, _performanceOptions, _logger);
        var partitionTasks = partitions
            .Select((channel, index) => ProcessPartitionAsync(index, channel.Reader, storageWriter, cancellationToken))
            .ToArray();

        Exception? processingError = null;

        try
        {
            await foreach (var tick in reader.ReadAllAsync(cancellationToken))
            {
                Interlocked.Increment(ref _consumedTickCount);

                var rawTick = BuildRawTick(tick);
                var normalized = _normalizer.Normalize(tick);
                if (!_filter.IsAllowed(normalized))
                {
                    continue;
                }

                if (_deduplicator.IsDuplicate(normalized))
                {
                    continue;
                }

                var partitionIndex = GetPartitionIndex(normalized.Symbol, partitionCount);
                await partitions[partitionIndex].Writer.WriteAsync(new PipelineItem(rawTick, normalized), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            processingError = ex;
            throw;
        }
        finally
        {
            foreach (var partition in partitions)
            {
                partition.Writer.TryComplete(processingError);
            }

            await Task.WhenAll(partitionTasks);
        }

        _logger.LogInformation("Pipeline stopped");
    }

    private async Task ProcessPartitionAsync(
        int partitionIndex,
        ChannelReader<PipelineItem> reader,
        BufferedStorageWriter storageWriter,
        CancellationToken cancellationToken)
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            var normalized = item.NormalizedTick;
            var now = DateTimeOffset.UtcNow;

            await storageWriter.StoreRawTickAsync(item.RawTick, cancellationToken);
            await storageWriter.StoreTickAsync(normalized, cancellationToken);

            var delay = now - normalized.Timestamp;
            _monitoring.RecordDelay(normalized.Source, delay);
            _monitoring.RecordTick(normalized.Source, normalized);
            _eventBus.PublishTick(normalized);

            MetricsSnapshot metrics;
            AggregatedCandle[] aggregates;
            lock (_aggregationSync)
            {
                metrics = _aggregationService.UpdateMetrics(normalized);
                aggregates = _aggregationService.Update(normalized).ToArray();
            }

            foreach (var candle in aggregates)
            {
                _monitoring.RecordAggregate(normalized.Source, candle);
                await storageWriter.StoreAggregateAsync(candle, cancellationToken);
                _eventBus.PublishAggregate(candle);
            }

            await _alertingGate.WaitAsync(cancellationToken);
            try
            {
                var alerts = await _alerting.HandleAsync(normalized, metrics, cancellationToken);
                foreach (var alert in alerts)
                {
                    _eventBus.PublishAlert(alert);
                }
            }
            finally
            {
                _alertingGate.Release();
            }
        }

        _logger.LogInformation("Pipeline partition {PartitionIndex} stopped", partitionIndex);
    }

    private static Channel<PipelineItem>[] CreatePartitions(int partitionCount, int capacity)
    {
        var channels = new Channel<PipelineItem>[partitionCount];
        for (var i = 0; i < partitionCount; i++)
        {
            channels[i] = Channel.CreateBounded<PipelineItem>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        return channels;
    }

    private static int GetPartitionIndex(string symbol, int partitionCount)
    {
        return Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(symbol) % partitionCount);
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

    private static string BuildPayload(Tick tick)
    {
        if (!string.IsNullOrWhiteSpace(tick.RawPayload))
        {
            return tick.RawPayload;
        }

        return MissingPayload;
    }

    private static string ExtractExchangeFromSource(string source)
    {
        var parts = source.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0] : source;
    }

    private readonly record struct PipelineItem(RawTick RawTick, NormalizedTick NormalizedTick);
}

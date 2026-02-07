using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Базовый источник рыночных данных через периодический REST polling сделок.
/// </summary>
public abstract class RestPollingSourceObsolete : IMarketDataSource
{
    private readonly ILogger _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;
    private readonly TradeCursorStore _tradeCursorStore;

    /// <summary>
    /// Инициализирует базовый REST polling-источник.
    /// </summary>
    /// <param name="logger">Логгер источника.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    /// <param name="storage">Слой хранения.</param>
    /// <param name="tradeCursorStore">Курсор сделок для дедупликации.</param>
    /// <param name="exchange">Биржа, к которой относится источник.</param>
    protected RestPollingSourceObsolete(
        ILogger logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore,
        MarketExchange exchange)
    {
        _logger = logger;
        _instrumentsConfig = instrumentsConfig;
        _storage = storage;
        _tradeCursorStore = tradeCursorStore;
        Exchange = exchange;
    }

    /// <summary>
    /// Имя источника.
    /// </summary>
    public string Name => $"{Exchange}-Rest";

    /// <summary>
    /// Биржа (торговая площадка), к которой относится источник.
    /// </summary>
    public MarketExchange Exchange { get; }

    /// <summary>
    /// Транспорт/тип подключения источника.
    /// </summary>
    public MarketDataSourceTransport Transport => MarketDataSourceTransport.Rest;

    /// <summary>
    /// Запускает цикл polling и публикует тики сделок в канал.
    /// </summary>
    /// <param name="writer">Канал для публикации тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения цикла polling.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(Exchange, MarketType.Spot);
        if (profile is null)
        {
            _logger.LogWarning("Не настроены инструменты для {Exchange} spot. REST polling остановлен.", Exchange);
            return;
        }

        var symbols = profile.Symbols?.ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("Профиль {Exchange} spot не содержит символов. REST polling остановлен.", Exchange);
            return;
        }

        await StoreInstrumentMetadataAsync(profile, cancellationToken);

        var pollInterval = profile.TargetUpdateInterval;
        var minDelay = TimeSpan.FromSeconds(1d / Math.Max(1, RequestsPerSecondLimit));

        _logger.LogInformation("{Exchange} REST polling source started ({Count} symbols)", Exchange, symbols.Length);

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var batch in symbols.Chunk(Math.Max(1, BatchSize)))
            {
                await PollWithRetryAsync(batch, writer, cancellationToken);
                await Task.Delay(minDelay, cancellationToken);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Запрашивает корректную остановку источника.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Выполняет polling для батча символов и возвращает 0..N тиков сделок.
    /// </summary>
    protected abstract Task<IReadOnlyCollection<Tick>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken ct);

    /// <summary>
    /// Размер батча символов на один polling-проход.
    /// </summary>
    protected virtual int BatchSize => 20;

    /// <summary>
    /// Ограничение по числу запросов в секунду (упрощённое).
    /// </summary>
    protected virtual int RequestsPerSecondLimit => 5;

    /// <summary>
    /// Максимальное число повторов при ошибке polling.
    /// </summary>
    protected virtual int MaxRetries => 3;

    /// <summary>
    /// Начальная задержка backoff при ошибке.
    /// </summary>
    protected virtual TimeSpan InitialBackoff => TimeSpan.FromMilliseconds(250);

    private async Task PollWithRetryAsync(IReadOnlyCollection<string> symbolsBatch, ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var backoff = InitialBackoff;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ticks = await PollBatchAsync(symbolsBatch, cancellationToken);
                foreach (var tick in ticks)
                {
                    var normalized = tick with
                    {
                        Source = Exchange.ToString()
                    };

                    if (!_tradeCursorStore.ShouldEmit(Exchange, normalized.Symbol, normalized.Timestamp, normalized.TradeId, normalized.Price, normalized.Volume))
                        continue;

                    await writer.WriteAsync(normalized, cancellationToken);
                    _tradeCursorStore.MarkEmitted(Exchange, normalized.Symbol, normalized.Timestamp, normalized.TradeId, normalized.Price, normalized.Volume);
                }

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                attempt++;
                _logger.LogWarning(ex, "{Exchange} REST polling error (attempt {Attempt}/{Max}); backing off", Exchange, attempt, MaxRetries);
                await Task.Delay(backoff, cancellationToken);
                backoff = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * 2);
            }
        }
    }

    private async Task StoreInstrumentMetadataAsync(MarketInstrumentProfile profile, CancellationToken cancellationToken)
    {
        foreach (var symbol in profile.Symbols)
        {
            var (baseAsset, quoteAsset) = ParseSymbol(symbol);
            await _storage.StoreInstrumentAsync(new InstrumentMetadata
            {
                Exchange = Exchange.ToString(),
                MarketType = profile.MarketType,
                Symbol = symbol,
                BaseAsset = baseAsset,
                QuoteAsset = quoteAsset,
                Description = $"{symbol} ({profile.MarketType})",
                PriceTickSize = 0.01m,
                VolumeStep = 0.0001m,
                PriceDecimals = 2,
                VolumeDecimals = 4,
                ContractSize = null,
                MinNotional = 10m
            }, cancellationToken);
        }
    }

    private static (string BaseAsset, string QuoteAsset) ParseSymbol(string symbol)
    {
        var parts = symbol
            .Split(new[] { '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            2 => (parts[0], parts[1]),
            _ => (symbol, "UNKNOWN")
        };
    }
}

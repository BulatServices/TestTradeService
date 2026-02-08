using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// РСЃС‚РѕС‡РЅРёРє СЂС‹РЅРѕС‡РЅС‹С… РґР°РЅРЅС‹С… С‡РµСЂРµР· РїРµСЂРёРѕРґРёС‡РµСЃРєРёР№ REST polling.
/// </summary>
public sealed class DemoRestPollingSource : IMarketDataSource
{
    private readonly ILogger<DemoRestPollingSource> _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;

    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·РёСЂСѓРµС‚ РёСЃС‚РѕС‡РЅРёРє REST polling.
    /// </summary>
    /// <param name="logger">Р›РѕРіРіРµСЂ РёСЃС‚РѕС‡РЅРёРєР°.</param>
    /// <param name="instrumentsConfig">РљРѕРЅС„РёРіСѓСЂР°С†РёСЏ РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ.</param>
    /// <param name="storage">РЎР»РѕР№ С…СЂР°РЅРµРЅРёСЏ.</param>
    public DemoRestPollingSource(ILogger<DemoRestPollingSource> logger, MarketInstrumentsConfig instrumentsConfig, IStorage storage)
    {
        _logger = logger;
        _instrumentsConfig = instrumentsConfig;
        _storage = storage;
    }

    /// <summary>
    /// РРјСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public string Name => "Demo-RestApi";

    /// <summary>
    /// Р‘РёСЂР¶Р° (С‚РѕСЂРіРѕРІР°СЏ РїР»РѕС‰Р°РґРєР°), Рє РєРѕС‚РѕСЂРѕР№ РѕС‚РЅРѕСЃРёС‚СЃСЏ РёСЃС‚РѕС‡РЅРёРє.
    /// </summary>
    public MarketExchange Exchange => MarketExchange.Demo;

    /// <summary>
    /// РўСЂР°РЅСЃРїРѕСЂС‚/С‚РёРї РїРѕРґРєР»СЋС‡РµРЅРёСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public MarketDataSourceTransport Transport => MarketDataSourceTransport.Rest;

    /// <summary>
    /// Р—Р°РїСѓСЃРєР°РµС‚ РіРµРЅРµСЂР°С†РёСЋ/С‡С‚РµРЅРёРµ С‚РёРєРѕРІ Рё РѕС‚РїСЂР°РІР»СЏРµС‚ РёС… РІ РєР°РЅР°Р».
    /// </summary>
    /// <param name="writer">РљР°РЅР°Р» РґР»СЏ РїСѓР±Р»РёРєР°С†РёРё С‚РёРєРѕРІ.</param>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    /// <returns>Р—Р°РґР°С‡Р° РІС‹РїРѕР»РЅРµРЅРёСЏ С†РёРєР»Р° polling.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(MarketExchange.Demo, MarketType.Spot, MarketDataSourceTransport.Rest);
        if (profile is null)
        {
            _logger.LogWarning("РќРµ РЅР°СЃС‚СЂРѕРµРЅС‹ РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹ РґР»СЏ СЂС‹РЅРєР° spot. REST polling РѕСЃС‚Р°РЅРѕРІР»РµРЅ.");
            return;
        }

        var symbols = profile.Symbols;
        var pollInterval = profile.TargetUpdateInterval;
        var random = Random.Shared;

        await StoreInstrumentMetadataAsync(profile, cancellationToken);

        _logger.LogInformation("REST polling source started");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var symbol in symbols)
            {
                var tick = new Tick
                {
                    Source = Name,
                    Symbol = symbol,
                    Price = 20_000m + (decimal)random.NextDouble() * 2_000m,
                    Volume = 0.1m + (decimal)random.NextDouble() * 5m,
                    Timestamp = now,
                    TradeId = $"rest-{now:O}-{random.Next(1_000_000)}"
                };

                await writer.WriteAsync(tick, cancellationToken);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

    }

    /// <summary>
    /// Р—Р°РїСЂР°С€РёРІР°РµС‚ РєРѕСЂСЂРµРєС‚РЅСѓСЋ РѕСЃС‚Р°РЅРѕРІРєСѓ РёСЃС‚РѕС‡РЅРёРєР°.
    /// Р”Р»СЏ polling-СЂРµР°Р»РёР·Р°С†РёРё РѕСЃРЅРѕРІРЅС‹Рј РјРµС…Р°РЅРёР·РјРѕРј РѕСЃС‚Р°РЅРѕРІРєРё СЏРІР»СЏРµС‚СЃСЏ РѕС‚РјРµРЅР° С‚РѕРєРµРЅР° РІ <see cref="StartAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

/// <summary>
/// Р‘Р°Р·РѕРІС‹Р№ РёСЃС‚РѕС‡РЅРёРє СЂС‹РЅРѕС‡РЅС‹С… РґР°РЅРЅС‹С… С‡РµСЂРµР· РїРµСЂРёРѕРґРёС‡РµСЃРєРёР№ REST polling СЃРґРµР»РѕРє.
/// </summary>
public abstract class RestPollingSource : IMarketDataSource
{
    private readonly ILogger _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;
    private readonly TradeCursorStore _tradeCursorStore;

    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·РёСЂСѓРµС‚ Р±Р°Р·РѕРІС‹Р№ REST polling-РёСЃС‚РѕС‡РЅРёРє.
    /// </summary>
    /// <param name="logger">Р›РѕРіРіРµСЂ РёСЃС‚РѕС‡РЅРёРєР°.</param>
    /// <param name="instrumentsConfig">РљРѕРЅС„РёРіСѓСЂР°С†РёСЏ РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ.</param>
    /// <param name="storage">РЎР»РѕР№ С…СЂР°РЅРµРЅРёСЏ.</param>
    /// <param name="tradeCursorStore">РљСѓСЂСЃРѕСЂ СЃРґРµР»РѕРє РґР»СЏ РґРµРґСѓРїР»РёРєР°С†РёРё.</param>
    /// <param name="exchange">Р‘РёСЂР¶Р°, Рє РєРѕС‚РѕСЂРѕР№ РѕС‚РЅРѕСЃРёС‚СЃСЏ РёСЃС‚РѕС‡РЅРёРє.</param>
    protected RestPollingSource(
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
    /// РРјСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public string Name => $"{Exchange}-Rest";

    /// <summary>
    /// Р‘РёСЂР¶Р° (С‚РѕСЂРіРѕРІР°СЏ РїР»РѕС‰Р°РґРєР°), Рє РєРѕС‚РѕСЂРѕР№ РѕС‚РЅРѕСЃРёС‚СЃСЏ РёСЃС‚РѕС‡РЅРёРє.
    /// </summary>
    public MarketExchange Exchange { get; }

    /// <summary>
    /// РўСЂР°РЅСЃРїРѕСЂС‚/С‚РёРї РїРѕРґРєР»СЋС‡РµРЅРёСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public MarketDataSourceTransport Transport => MarketDataSourceTransport.Rest;

    /// <summary>
    /// Р—Р°РїСѓСЃРєР°РµС‚ С†РёРєР» polling Рё РїСѓР±Р»РёРєСѓРµС‚ С‚РёРєРё СЃРґРµР»РѕРє РІ РєР°РЅР°Р».
    /// </summary>
    /// <param name="writer">РљР°РЅР°Р» РґР»СЏ РїСѓР±Р»РёРєР°С†РёРё С‚РёРєРѕРІ.</param>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    /// <returns>Р—Р°РґР°С‡Р° РІС‹РїРѕР»РЅРµРЅРёСЏ С†РёРєР»Р° polling.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(Exchange, MarketType.Spot, MarketDataSourceTransport.Rest);
        if (profile is null)
        {
            _logger.LogWarning("РќРµ РЅР°СЃС‚СЂРѕРµРЅС‹ РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹ РґР»СЏ {Exchange} spot. REST polling РѕСЃС‚Р°РЅРѕРІР»РµРЅ.", Exchange);
            return;
        }

        var symbols = profile.Symbols?.ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("РџСЂРѕС„РёР»СЊ {Exchange} spot РЅРµ СЃРѕРґРµСЂР¶РёС‚ СЃРёРјРІРѕР»РѕРІ. REST polling РѕСЃС‚Р°РЅРѕРІР»РµРЅ.", Exchange);
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
    /// Р—Р°РїСЂР°С€РёРІР°РµС‚ РєРѕСЂСЂРµРєС‚РЅСѓСЋ РѕСЃС‚Р°РЅРѕРІРєСѓ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Р’С‹РїРѕР»РЅСЏРµС‚ polling РґР»СЏ Р±Р°С‚С‡Р° СЃРёРјРІРѕР»РѕРІ Рё РІРѕР·РІСЂР°С‰Р°РµС‚ 0..N С‚РёРєРѕРІ СЃРґРµР»РѕРє.
    /// </summary>
    protected abstract Task<IReadOnlyCollection<Tick>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken ct);

    /// <summary>
    /// Р Р°Р·РјРµСЂ Р±Р°С‚С‡Р° СЃРёРјРІРѕР»РѕРІ РЅР° РѕРґРёРЅ polling-РїСЂРѕС…РѕРґ.
    /// </summary>
    protected virtual int BatchSize => 20;

    /// <summary>
    /// РћРіСЂР°РЅРёС‡РµРЅРёРµ РїРѕ С‡РёСЃР»Сѓ Р·Р°РїСЂРѕСЃРѕРІ РІ СЃРµРєСѓРЅРґСѓ (СѓРїСЂРѕС‰С‘РЅРЅРѕРµ).
    /// </summary>
    protected virtual int RequestsPerSecondLimit => 5;

    /// <summary>
    /// РњР°РєСЃРёРјР°Р»СЊРЅРѕРµ С‡РёСЃР»Рѕ РїРѕРІС‚РѕСЂРѕРІ РїСЂРё РѕС€РёР±РєРµ polling.
    /// </summary>
    protected virtual int MaxRetries => 3;

    /// <summary>
    /// РќР°С‡Р°Р»СЊРЅР°СЏ Р·Р°РґРµСЂР¶РєР° backoff РїСЂРё РѕС€РёР±РєРµ.
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
                        Source = Name
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


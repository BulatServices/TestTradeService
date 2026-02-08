using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// РСЃС‚РѕС‡РЅРёРє СЂС‹РЅРѕС‡РЅС‹С… РґР°РЅРЅС‹С… С‡РµСЂРµР· WebSocket-РїРѕС‚РѕРє.
/// </summary>
public sealed class LegacyWebSocketSource : IMarketDataSource
{
    private readonly ILogger<LegacyWebSocketSource> _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;

    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·РёСЂСѓРµС‚ РёСЃС‚РѕС‡РЅРёРє WebSocket.
    /// </summary>
    /// <param name="logger">Р›РѕРіРіРµСЂ РёСЃС‚РѕС‡РЅРёРєР°.</param>
    /// <param name="instrumentsConfig">РљРѕРЅС„РёРіСѓСЂР°С†РёСЏ РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ.</param>
    /// <param name="storage">РЎР»РѕР№ С…СЂР°РЅРµРЅРёСЏ.</param>
    public LegacyWebSocketSource(ILogger<LegacyWebSocketSource> logger, MarketInstrumentsConfig instrumentsConfig, IStorage storage)
    {
        _logger = logger;
        _instrumentsConfig = instrumentsConfig;
        _storage = storage;
    }

    /// <summary>
    /// РРјСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public string Name => "Legacy-WebSocket";

    /// <summary>
    /// Р‘РёСЂР¶Р° (С‚РѕСЂРіРѕРІР°СЏ РїР»РѕС‰Р°РґРєР°), Рє РєРѕС‚РѕСЂРѕР№ РѕС‚РЅРѕСЃРёС‚СЃСЏ РёСЃС‚РѕС‡РЅРёРє.
    /// </summary>
    public MarketExchange Exchange => MarketExchange.Demo;

    /// <summary>
    /// РўСЂР°РЅСЃРїРѕСЂС‚/С‚РёРї РїРѕРґРєР»СЋС‡РµРЅРёСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public MarketDataSourceTransport Transport => MarketDataSourceTransport.WebSocket;

    /// <summary>
    /// Р—Р°РїСѓСЃРєР°РµС‚ РїРѕС‚РѕРєРѕРІСѓСЋ РїСѓР±Р»РёРєР°С†РёСЋ С‚РёРєРѕРІ РІ РєР°РЅР°Р».
    /// </summary>
    /// <param name="writer">РљР°РЅР°Р» РґР»СЏ РїСѓР±Р»РёРєР°С†РёРё С‚РёРєРѕРІ.</param>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    /// <returns>Р—Р°РґР°С‡Р° РІС‹РїРѕР»РЅРµРЅРёСЏ РїРѕС‚РѕРєРѕРІРѕР№ РїСѓР±Р»РёРєР°С†РёРё.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(MarketExchange.Demo, MarketType.Perp, MarketDataSourceTransport.WebSocket);
        if (profile is null)
        {
            _logger.LogWarning("РќРµ РЅР°СЃС‚СЂРѕРµРЅС‹ РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹ РґР»СЏ СЂС‹РЅРєР° perp. WebSocket-РїРѕС‚РѕРє РѕСЃС‚Р°РЅРѕРІР»РµРЅ.");
            return;
        }

        var symbols = profile.Symbols?.ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("РџСЂРѕС„РёР»СЊ perp РЅРµ СЃРѕРґРµСЂР¶РёС‚ СЃРёРјРІРѕР»РѕРІ. WebSocket-РїРѕС‚РѕРє РѕСЃС‚Р°РЅРѕРІР»РµРЅ.");
            return;
        }
        var tickInterval = profile.TargetUpdateInterval;
        var random = Random.Shared;

        await StoreInstrumentMetadataAsync(profile, cancellationToken);

        _logger.LogInformation("WebSocket source started");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var symbol = symbols[random.Next(symbols.Length)];
            var tick = new Tick
            {
                Source = Name,
                Symbol = symbol,
                Price = 20_000m + (decimal)random.NextDouble() * 2_500m,
                Volume = 0.05m + (decimal)random.NextDouble() * 3m,
                Timestamp = now,
                TradeId = $"ws-{now:O}-{random.Next(1_000_000)}"
            };

            await writer.WriteAsync(tick, cancellationToken);
            await Task.Delay(tickInterval, cancellationToken);
        }

    }

    /// <summary>
    /// Р—Р°РїСЂР°С€РёРІР°РµС‚ РєРѕСЂСЂРµРєС‚РЅСѓСЋ РѕСЃС‚Р°РЅРѕРІРєСѓ РёСЃС‚РѕС‡РЅРёРєР°.
    /// Р”Р»СЏ WebSocket-СЂРµР°Р»РёР·Р°С†РёРё Р·РґРµСЃСЊ РѕР±С‹С‡РЅРѕ Р·Р°РєСЂС‹РІР°СЋС‚СЃСЏ СЃРѕРµРґРёРЅРµРЅРёСЏ Рё РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ РѕС‚РїРёСЃРєР°.
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
                PriceTickSize = 0.1m,
                VolumeStep = 0.001m,
                PriceDecimals = 1,
                VolumeDecimals = 3,
                ContractSize = 1m,
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
/// Р‘Р°Р·РѕРІС‹Р№ РёСЃС‚РѕС‡РЅРёРє СЂС‹РЅРѕС‡РЅС‹С… РґР°РЅРЅС‹С… С‡РµСЂРµР· WebSocket-РїРѕС‚РѕРє СЃРґРµР»РѕРє.
/// </summary>
public abstract class WebSocketSource : IMarketDataSource
{
    private readonly ILogger _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;
    private readonly TradeCursorStore _tradeCursorStore;

    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·РёСЂСѓРµС‚ Р±Р°Р·РѕРІС‹Р№ WebSocket-РёСЃС‚РѕС‡РЅРёРє.
    /// </summary>
    /// <param name="logger">Р›РѕРіРіРµСЂ РёСЃС‚РѕС‡РЅРёРєР°.</param>
    /// <param name="instrumentsConfig">РљРѕРЅС„РёРіСѓСЂР°С†РёСЏ РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ.</param>
    /// <param name="storage">РЎР»РѕР№ С…СЂР°РЅРµРЅРёСЏ.</param>
    /// <param name="tradeCursorStore">РљСѓСЂСЃРѕСЂ СЃРґРµР»РѕРє РґР»СЏ РґРµРґСѓРїР»РёРєР°С†РёРё.</param>
    /// <param name="exchange">Р‘РёСЂР¶Р°, Рє РєРѕС‚РѕСЂРѕР№ РѕС‚РЅРѕСЃРёС‚СЃСЏ РёСЃС‚РѕС‡РЅРёРє.</param>
    protected WebSocketSource(
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
    public string Name => $"{Exchange}-WebSocket";

    /// <summary>
    /// Р‘РёСЂР¶Р° (С‚РѕСЂРіРѕРІР°СЏ РїР»РѕС‰Р°РґРєР°), Рє РєРѕС‚РѕСЂРѕР№ РѕС‚РЅРѕСЃРёС‚СЃСЏ РёСЃС‚РѕС‡РЅРёРє.
    /// </summary>
    public MarketExchange Exchange { get; }

    /// <summary>
    /// РўСЂР°РЅСЃРїРѕСЂС‚/С‚РёРї РїРѕРґРєР»СЋС‡РµРЅРёСЏ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    public MarketDataSourceTransport Transport => MarketDataSourceTransport.WebSocket;

    /// <summary>
    /// Р—Р°РїСѓСЃРєР°РµС‚ WebSocket-РёСЃС‚РѕС‡РЅРёРє Рё РїСѓР±Р»РёРєСѓРµС‚ С‚РёРєРё СЃРґРµР»РѕРє РІ РєР°РЅР°Р».
    /// </summary>
    /// <param name="writer">РљР°РЅР°Р» РґР»СЏ РїСѓР±Р»РёРєР°С†РёРё С‚РёРєРѕРІ.</param>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    /// <returns>Р—Р°РґР°С‡Р° РІС‹РїРѕР»РЅРµРЅРёСЏ РёСЃС‚РѕС‡РЅРёРєР°.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(Exchange, MarketType.Spot, MarketDataSourceTransport.WebSocket);
        if (profile is null)
        {
            _logger.LogWarning("РќРµ РЅР°СЃС‚СЂРѕРµРЅС‹ РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹ РґР»СЏ {Exchange} spot. WebSocket-РёСЃС‚РѕС‡РЅРёРє РѕСЃС‚Р°РЅРѕРІР»РµРЅ.", Exchange);
            return;
        }

        var symbols = profile.Symbols?.ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("РџСЂРѕС„РёР»СЊ {Exchange} spot РЅРµ СЃРѕРґРµСЂР¶РёС‚ СЃРёРјРІРѕР»РѕРІ. WebSocket-РёСЃС‚РѕС‡РЅРёРє РѕСЃС‚Р°РЅРѕРІР»РµРЅ.", Exchange);
            return;
        }

        await StoreInstrumentMetadataAsync(profile, cancellationToken);

        _logger.LogInformation("{Exchange} WebSocket source started ({Count} symbols)", Exchange, symbols.Length);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

                var endpoint = BuildEndpoint();
                await socket.ConnectAsync(endpoint, cancellationToken);

                await SendSubscribeAsync(socket, symbols, cancellationToken);
                await ReceiveLoopAsync(socket, writer, cancellationToken);

                await SendUnsubscribeAsync(socket, symbols, cancellationToken);
                await CloseAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Exchange} WebSocket source error; reconnecting", Exchange);
                await Task.Delay(ReconnectDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Р—Р°РїСЂР°С€РёРІР°РµС‚ РєРѕСЂСЂРµРєС‚РЅСѓСЋ РѕСЃС‚Р°РЅРѕРІРєСѓ РёСЃС‚РѕС‡РЅРёРєР°.
    /// </summary>
    /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Р’РѕР·РІСЂР°С‰Р°РµС‚ WebSocket endpoint РґР»СЏ РїРѕРґРєР»СЋС‡РµРЅРёСЏ.
    /// </summary>
    protected abstract Uri BuildEndpoint();

    /// <summary>
    /// РћС‚РїСЂР°РІР»СЏРµС‚ СЃРѕРѕР±С‰РµРЅРёРµ РїРѕРґРїРёСЃРєРё РЅР° СѓРєР°Р·Р°РЅРЅС‹Рµ СЃРёРјРІРѕР»С‹.
    /// </summary>
    protected abstract Task SendSubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct);

    /// <summary>
    /// РћС‚РїСЂР°РІР»СЏРµС‚ СЃРѕРѕР±С‰РµРЅРёРµ РѕС‚РїРёСЃРєРё РѕС‚ СѓРєР°Р·Р°РЅРЅС‹С… СЃРёРјРІРѕР»РѕРІ.
    /// </summary>
    protected abstract Task SendUnsubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct);

    /// <summary>
    /// РџР°СЂСЃРёС‚ payload WebSocket-СЃРѕРѕР±С‰РµРЅРёСЏ Рё РІРѕР·РІСЂР°С‰Р°РµС‚ 0..N С‚РёРєРѕРІ СЃРґРµР»РѕРє.
    /// </summary>
    protected abstract IReadOnlyCollection<Tick> ParseTrades(string payload, DateTimeOffset receivedAt);

    /// <summary>
    /// Р—Р°РґРµСЂР¶РєР° РїРµСЂРµРґ РїРµСЂРµРїРѕРґРєР»СЋС‡РµРЅРёРµРј РїСЂРё РѕС€РёР±РєРµ.
    /// </summary>
    protected virtual TimeSpan ReconnectDelay => TimeSpan.FromSeconds(2);

    private async Task ReceiveLoopAsync(ClientWebSocket socket, ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var payload = await ReceiveTextMessageAsync(socket, cancellationToken);
            if (payload is null)
                return;

            var receivedAt = DateTimeOffset.UtcNow;
            var ticks = ParseTrades(payload, receivedAt);
            if (ticks.Count == 0)
                continue;

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
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
            return null;

        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        try
        {
            var builder = new StringBuilder(8 * 1024);
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                if (result.Count > 0)
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                    return builder.ToString();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task CloseAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", cancellationToken);
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


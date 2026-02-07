using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Coinbase;

/// <summary>
/// WebSocket-источник сделок Coinbase Exchange (канал <c>matches</c>).
/// </summary>
public sealed class CoinbaseMatchesWebSocketSource : WebSocketSource
{
    private static readonly Uri DefaultEndpoint = new("wss://ws-feed.exchange.coinbase.com");

    /// <summary>
    /// Инициализирует WebSocket-источник сделок Coinbase Exchange.
    /// </summary>
    public CoinbaseMatchesWebSocketSource(
        ILogger<CoinbaseMatchesWebSocketSource> logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore)
        : base(logger, instrumentsConfig, storage, tradeCursorStore, MarketExchange.Coinbase)
    {
    }

    /// <inheritdoc />
    protected override Uri BuildEndpoint() => DefaultEndpoint;

    /// <inheritdoc />
    protected override Task SendSubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var productsJson = string.Join(',', symbols.Select(s => $"\"{s}\""));
        var payload = $"{{\"type\":\"subscribe\",\"product_ids\":[{productsJson}],\"channels\":[\"matches\"]}}";
        return SendTextAsync(socket, payload, ct);
    }

    /// <inheritdoc />
    protected override Task SendUnsubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var productsJson = string.Join(',', symbols.Select(s => $"\"{s}\""));
        var payload = $"{{\"type\":\"unsubscribe\",\"product_ids\":[{productsJson}],\"channels\":[\"matches\"]}}";
        return SendTextAsync(socket, payload, ct);
    }

    /// <inheritdoc />
    protected override IReadOnlyCollection<Tick> ParseTrades(string payload, DateTimeOffset receivedAt)
    {
        return CoinbaseTradesParsing.ParseWebSocketMatches(payload, receivedAt);
    }

    private static Task SendTextAsync(ClientWebSocket socket, string payload, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open)
            return Task.CompletedTask;

        var bytes = Encoding.UTF8.GetBytes(payload);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}


using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Kraken;

/// <summary>
/// WebSocket-источник сделок Kraken (поток <c>trade</c>).
/// </summary>
public sealed class KrakenTradesWebSocketSource : WebSocketSource
{
    private static readonly Uri DefaultEndpoint = new("wss://ws.kraken.com");

    /// <summary>
    /// Инициализирует WebSocket-источник сделок Kraken.
    /// </summary>
    public KrakenTradesWebSocketSource(
        ILogger<KrakenTradesWebSocketSource> logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore)
        : base(logger, instrumentsConfig, storage, tradeCursorStore, MarketExchange.Kraken)
    {
    }

    /// <inheritdoc />
    protected override Uri BuildEndpoint() => DefaultEndpoint;

    /// <inheritdoc />
    protected override Task SendSubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var pairsJson = string.Join(',', symbols.Select(s => $"\"{s}\""));
        var payload = $"{{\"event\":\"subscribe\",\"pair\":[{pairsJson}],\"subscription\":{{\"name\":\"trade\"}}}}";
        return SendTextAsync(socket, payload, ct);
    }

    /// <inheritdoc />
    protected override Task SendUnsubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var pairsJson = string.Join(',', symbols.Select(s => $"\"{s}\""));
        var payload = $"{{\"event\":\"unsubscribe\",\"pair\":[{pairsJson}],\"subscription\":{{\"name\":\"trade\"}}}}";
        return SendTextAsync(socket, payload, ct);
    }

    /// <inheritdoc />
    protected override IReadOnlyCollection<Tick> ParseTrades(string payload, DateTimeOffset receivedAt)
    {
        return KrakenTradesParsing.ParseWebSocketTrades(payload, receivedAt);
    }

    private static Task SendTextAsync(ClientWebSocket socket, string payload, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open)
            return Task.CompletedTask;

        var bytes = Encoding.UTF8.GetBytes(payload);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}


using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Bybit;

/// <summary>
/// WebSocket-источник публичных сделок Bybit (v5 publicTrade).
/// </summary>
public sealed class BybitPublicTradesWebSocketSource : WebSocketSource
{
    private static readonly Uri DefaultEndpoint = new("wss://stream.bybit.com/v5/public/spot");

    /// <summary>
    /// Инициализирует WebSocket-источник публичных сделок Bybit.
    /// </summary>
    public BybitPublicTradesWebSocketSource(
        ILogger<BybitPublicTradesWebSocketSource> logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore)
        : base(logger, instrumentsConfig, storage, tradeCursorStore, MarketExchange.Bybit)
    {
    }

    /// <inheritdoc />
    protected override Uri BuildEndpoint() => DefaultEndpoint;

    /// <inheritdoc />
    protected override Task SendSubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var args = string.Join(',', symbols.Select(s => $"\"publicTrade.{s}\""));
        var payload = $"{{\"op\":\"subscribe\",\"args\":[{args}]}}";
        return SendTextAsync(socket, payload, ct);
    }

    /// <inheritdoc />
    protected override Task SendUnsubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var args = string.Join(',', symbols.Select(s => $"\"publicTrade.{s}\""));
        var payload = $"{{\"op\":\"unsubscribe\",\"args\":[{args}]}}";
        return SendTextAsync(socket, payload, ct);
    }

    /// <inheritdoc />
    protected override IReadOnlyCollection<Tick> ParseTrades(string payload, DateTimeOffset receivedAt)
    {
        return BybitTradesParsing.ParseWebSocketPublicTrades(payload, receivedAt);
    }

    private static Task SendTextAsync(ClientWebSocket socket, string payload, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open)
            return Task.CompletedTask;

        var bytes = Encoding.UTF8.GetBytes(payload);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}


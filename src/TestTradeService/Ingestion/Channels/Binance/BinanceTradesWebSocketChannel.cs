using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Core;

namespace TestTradeService.Ingestion.Channels.Binance;

/// <summary>
/// Конечная реализация WebSocket-канала Binance для потока сделок.
/// </summary>
public sealed class BinanceTradesWebSocketChannel : WebSocketChannelBase
{
    private readonly WebSocketChannelSettings _settings;
    private readonly ILogger<BinanceTradesWebSocketChannel> _logger;
    private ClientWebSocket? _socket;

    /// <summary>
    /// Инициализирует WebSocket-канал сделок Binance.
    /// </summary>
    public BinanceTradesWebSocketChannel(
        string id,
        IReadOnlyCollection<string> symbols,
        WebSocketChannelSettings settings,
        ILogger<BinanceTradesWebSocketChannel> logger)
        : base(id, "Binance", symbols, settings, logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(_settings.Endpoint), cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_socket is null)
            return;

        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", cancellationToken);

        _socket.Dispose();
        _socket = null;
    }

    /// <inheritdoc />
    protected override Task SubscribeAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken)
    {
        var paramsArray = string.Join(',', symbols.Select(s => $"\"{s.ToLowerInvariant()}@trade\""));
        var request = $"{{\"method\":\"SUBSCRIBE\",\"params\":[{paramsArray}],\"id\":1}}";
        return SendTextAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task UnsubscribeAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken)
    {
        var paramsArray = string.Join(',', symbols.Select(s => $"\"{s.ToLowerInvariant()}@trade\""));
        var request = $"{{\"method\":\"UNSUBSCRIBE\",\"params\":[{paramsArray}],\"id\":2}}";
        return SendTextAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task SendPingAsync(CancellationToken cancellationToken)
    {
        if (_socket?.State != WebSocketState.Open)
            return Task.CompletedTask;

        return _socket.SendAsync(new ArraySegment<byte>(Array.Empty<byte>()), WebSocketMessageType.Binary, true, cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task<string?> ReceivePayloadAsync(CancellationToken cancellationToken)
    {
        if (_socket?.State != WebSocketState.Open)
            return null;

        var buffer = new byte[4096];
        var result = await _socket.ReceiveAsync(buffer, cancellationToken);

        if (result.MessageType == WebSocketMessageType.Close)
            throw new WebSocketException("Socket closed by remote peer");

        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        _logger.LogDebug("Binance raw payload received: {Payload}", text);
        return text;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> BuildMetadata()
    {
        return new Dictionary<string, string>
        {
            ["endpoint"] = _settings.Endpoint,
            ["subscription"] = "trade"
        };
    }

    private Task SendTextAsync(string payload, CancellationToken cancellationToken)
    {
        if (_socket?.State != WebSocketState.Open)
            return Task.CompletedTask;

        var bytes = Encoding.UTF8.GetBytes(payload);
        return _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }
}

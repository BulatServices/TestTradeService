using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Core;

/// <summary>
/// Базовый класс для WebSocket-каналов.
/// </summary>
public abstract class WebSocketChannelBase : BaseChannel
{
    private readonly WebSocketChannelSettings _settings;
    private DateTimeOffset _lastMessageAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Инициализирует WebSocket-канал.
    /// </summary>
    protected WebSocketChannelBase(string id, string exchange, IReadOnlyCollection<string> symbols, WebSocketChannelSettings settings, ILogger logger)
        : base(id, exchange, ChannelKind.WebSocket, symbols, logger)
    {
        _settings = settings;
    }

    /// <inheritdoc />
    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken);
                await PublishStatusAsync(SourceConnectivityStatus.Online);
                await SubscribeAsync(Symbols, cancellationToken);

                var heartbeatTask = RunHeartbeatAsync(cancellationToken);
                var receiveTask = ReceiveLoopAsync(cancellationToken);

                await Task.WhenAny(heartbeatTask, receiveTask);

                await UnsubscribeAsync(Symbols, cancellationToken);
                await DisconnectAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                IncrementReconnectCounter();
                await PublishErrorAsync(ex);
            }

            await PublishStatusAsync(SourceConnectivityStatus.Reconnecting);
            await Task.Delay(_settings.ReconnectDelay, cancellationToken);
        }
    }

    /// <summary>
    /// Устанавливает WebSocket-соединение.
    /// </summary>
    protected abstract Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Корректно закрывает WebSocket-соединение.
    /// </summary>
    protected abstract Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Подписывает канал на заданные символы.
    /// </summary>
    protected abstract Task SubscribeAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken);

    /// <summary>
    /// Отписывает канал от заданных символов.
    /// </summary>
    protected abstract Task UnsubscribeAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет ping/heartbeat.
    /// </summary>
    protected abstract Task SendPingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Считывает очередной payload из сокета.
    /// </summary>
    protected abstract Task<string?> ReceivePayloadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает транспортные метаданные сообщения.
    /// </summary>
    protected abstract IReadOnlyDictionary<string, string> BuildMetadata();

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_settings.HeartbeatInterval, cancellationToken);
            await SendPingAsync(cancellationToken);

            if (DateTimeOffset.UtcNow - _lastMessageAt > _settings.StallTimeout)
                throw new TimeoutException($"Channel {Id} detected data stall");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var payload = await ReceivePayloadAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
                continue;

            _lastMessageAt = DateTimeOffset.UtcNow;
            var message = new RawMessage
            {
                Exchange = Exchange,
                ChannelId = Id,
                TransportType = TransportType.WebSocket,
                ReceivedAt = _lastMessageAt,
                Payload = payload,
                Metadata = BuildMetadata()
            };

            await PublishRawMessageAsync(message);
        }
    }
}

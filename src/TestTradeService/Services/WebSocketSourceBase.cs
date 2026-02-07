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
/// Базовый источник рыночных данных через WebSocket-поток сделок.
/// </summary>
public abstract class WebSocketSourceObsolete : IMarketDataSource
{
    private readonly ILogger _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;
    private readonly TradeCursorStore _tradeCursorStore;

    /// <summary>
    /// Инициализирует базовый WebSocket-источник.
    /// </summary>
    /// <param name="logger">Логгер источника.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    /// <param name="storage">Слой хранения.</param>
    /// <param name="tradeCursorStore">Курсор сделок для дедупликации.</param>
    /// <param name="exchange">Биржа, к которой относится источник.</param>
    protected WebSocketSourceObsolete(
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
    public string Name => $"{Exchange}-WebSocket";

    /// <summary>
    /// Биржа (торговая площадка), к которой относится источник.
    /// </summary>
    public MarketExchange Exchange { get; }

    /// <summary>
    /// Транспорт/тип подключения источника.
    /// </summary>
    public MarketDataSourceTransport Transport => MarketDataSourceTransport.WebSocket;

    /// <summary>
    /// Запускает WebSocket-источник и публикует тики сделок в канал.
    /// </summary>
    /// <param name="writer">Канал для публикации тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения источника.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(Exchange, MarketType.Spot);
        if (profile is null)
        {
            _logger.LogWarning("Не настроены инструменты для {Exchange} spot. WebSocket-источник остановлен.", Exchange);
            return;
        }

        var symbols = profile.Symbols?.ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("Профиль {Exchange} spot не содержит символов. WebSocket-источник остановлен.", Exchange);
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
    /// Запрашивает корректную остановку источника.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Возвращает WebSocket endpoint для подключения.
    /// </summary>
    protected abstract Uri BuildEndpoint();

    /// <summary>
    /// Отправляет сообщение подписки на указанные символы.
    /// </summary>
    protected abstract Task SendSubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct);

    /// <summary>
    /// Отправляет сообщение отписки от указанных символов.
    /// </summary>
    protected abstract Task SendUnsubscribeAsync(ClientWebSocket socket, IReadOnlyCollection<string> symbols, CancellationToken ct);

    /// <summary>
    /// Парсит payload WebSocket-сообщения и возвращает 0..N тиков сделок.
    /// </summary>
    protected abstract IReadOnlyCollection<Tick> ParseTrades(string payload, DateTimeOffset receivedAt);

    /// <summary>
    /// Задержка перед переподключением при ошибке.
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
                    Source = Exchange.ToString()
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

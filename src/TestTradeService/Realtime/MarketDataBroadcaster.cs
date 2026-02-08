using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Realtime;

/// <summary>
/// Фоновый транслятор событий шины в SignalR-клиентов.
/// </summary>
public sealed class MarketDataBroadcaster : BackgroundService
{
    private readonly IMarketDataEventBus _eventBus;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly MarketHubConnectionStateStore _stateStore;
    private readonly ILogger<MarketDataBroadcaster> _logger;

    /// <summary>
    /// Инициализирует фоновый транслятор SignalR.
    /// </summary>
    /// <param name="eventBus">Шина событий рыночного потока.</param>
    /// <param name="hubContext">Контекст SignalR-хаба.</param>
    /// <param name="stateStore">Хранилище подписок клиентов.</param>
    /// <param name="logger">Логгер транслятора.</param>
    public MarketDataBroadcaster(
        IMarketDataEventBus eventBus,
        IHubContext<MarketDataHub> hubContext,
        MarketHubConnectionStateStore stateStore,
        ILogger<MarketDataBroadcaster> logger)
    {
        _eventBus = eventBus;
        _hubContext = hubContext;
        _stateStore = stateStore;
        _logger = logger;
    }

    /// <summary>
    /// Читает события из шины и рассылает их подписанным клиентам.
    /// </summary>
    /// <param name="stoppingToken">Токен остановки hosted service.</param>
    /// <returns>Задача выполнения фоновой трансляции.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _eventBus.ReadAllAsync(stoppingToken))
        {
            try
            {
                await BroadcastAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast SignalR event {EventKind}", item.Kind);
            }
        }
    }

    private Task BroadcastAsync(MarketDataEvent item, CancellationToken cancellationToken)
    {
        return item.Kind switch
        {
            "tick" when item.Payload is NormalizedTick tick => BroadcastTickAsync(tick, cancellationToken),
            "aggregate" => _hubContext.Clients.All.SendAsync("aggregate", item.Payload, cancellationToken),
            "monitoring" => _hubContext.Clients.All.SendAsync("monitoring", item.Payload, cancellationToken),
            "alert" => _hubContext.Clients.All.SendAsync("alert", item.Payload, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    private async Task BroadcastTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        var snapshot = _stateStore.Snapshot();
        if (snapshot.Count == 0)
        {
            return;
        }

        var payload = new
        {
            source = tick.Source,
            exchange = ExtractExchange(tick.Source),
            symbol = tick.Symbol,
            price = tick.Price,
            volume = tick.Volume,
            timestamp = tick.Timestamp,
            receivedAt = DateTimeOffset.UtcNow
        };

        foreach (var connection in snapshot)
        {
            if (!Matches(connection.Value, payload.exchange, tick.Symbol))
            {
                continue;
            }

            await _hubContext.Clients.Client(connection.Key).SendAsync("tick", payload, cancellationToken);
        }
    }

    private static bool Matches(ClientSubscriptionSnapshot state, string exchange, string symbol)
    {
        if (!string.IsNullOrWhiteSpace(state.Exchange)
            && !string.Equals(state.Exchange, exchange, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(state.Symbol)
            && !string.Equals(state.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (state.Symbols.Count == 0)
        {
            return true;
        }

        return state.Symbols.Any(x => string.Equals(x, symbol, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractExchange(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "Unknown";
        }

        var idx = source.IndexOf('-', StringComparison.Ordinal);
        return idx <= 0 ? source : source[..idx];
    }
}

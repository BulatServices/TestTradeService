using System.Collections.Concurrent;

namespace TestTradeService.Realtime;

/// <summary>
/// Хранилище фильтров и подписок SignalR-подключений.
/// </summary>
public sealed class MarketHubConnectionStateStore
{
    private readonly ConcurrentDictionary<string, ClientSubscriptionState> _states = new(StringComparer.Ordinal);

    /// <summary>
    /// Устанавливает фильтр подключения.
    /// </summary>
    /// <param name="connectionId">Идентификатор подключения.</param>
    /// <param name="exchange">Фильтр по бирже.</param>
    /// <param name="symbol">Фильтр по символу.</param>
    public void SetFilter(string connectionId, string? exchange, string? symbol)
    {
        var state = _states.GetOrAdd(connectionId, _ => new ClientSubscriptionState());
        state.Exchange = Normalize(exchange);
        state.Symbol = Normalize(symbol);
    }

    /// <summary>
    /// Добавляет подписки на символы.
    /// </summary>
    /// <param name="connectionId">Идентификатор подключения.</param>
    /// <param name="symbols">Список символов.</param>
    public void SubscribeSymbols(string connectionId, IReadOnlyCollection<string> symbols)
    {
        var state = _states.GetOrAdd(connectionId, _ => new ClientSubscriptionState());
        lock (state.Sync)
        {
            foreach (var symbol in symbols)
            {
                var normalized = Normalize(symbol);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    state.Symbols.Add(normalized);
                }
            }
        }
    }

    /// <summary>
    /// Удаляет подписки на символы.
    /// </summary>
    /// <param name="connectionId">Идентификатор подключения.</param>
    /// <param name="symbols">Список символов.</param>
    public void UnsubscribeSymbols(string connectionId, IReadOnlyCollection<string> symbols)
    {
        if (!_states.TryGetValue(connectionId, out var state))
        {
            return;
        }

        lock (state.Sync)
        {
            foreach (var symbol in symbols)
            {
                var normalized = Normalize(symbol);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    state.Symbols.Remove(normalized);
                }
            }
        }
    }

    /// <summary>
    /// Удаляет состояние подключения.
    /// </summary>
    /// <param name="connectionId">Идентификатор подключения.</param>
    public void Remove(string connectionId)
    {
        _states.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Возвращает снимок состояний всех подключений.
    /// </summary>
    /// <returns>Набор состояний по connection id.</returns>
    public IReadOnlyDictionary<string, ClientSubscriptionSnapshot> Snapshot()
    {
        var snapshot = new Dictionary<string, ClientSubscriptionSnapshot>(StringComparer.Ordinal);
        foreach (var pair in _states)
        {
            lock (pair.Value.Sync)
            {
                snapshot[pair.Key] = new ClientSubscriptionSnapshot(
                    pair.Value.Exchange,
                    pair.Value.Symbol,
                    pair.Value.Symbols.ToArray());
            }
        }

        return snapshot;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class ClientSubscriptionState
    {
        public object Sync { get; } = new();
        public string? Exchange { get; set; }
        public string? Symbol { get; set; }
        public HashSet<string> Symbols { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Снимок подписок одного SignalR-подключения.
/// </summary>
/// <param name="Exchange">Фильтр по бирже.</param>
/// <param name="Symbol">Фильтр по символу.</param>
/// <param name="Symbols">Явные подписки на символы.</param>
public sealed record ClientSubscriptionSnapshot(string? Exchange, string? Symbol, IReadOnlyCollection<string> Symbols);

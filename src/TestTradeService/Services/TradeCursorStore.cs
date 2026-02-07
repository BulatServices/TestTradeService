using System.Collections.Concurrent;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// In-memory курсор сделок для дедупликации между WebSocket и REST источниками в рамках одной биржи/символа.
/// </summary>
public sealed class TradeCursorStore
{
    private readonly ConcurrentDictionary<TradeKey, TradeCursorState> _stateByKey = new();
    private readonly TimeSpan _fingerprintTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Определяет, нужно ли публиковать тик (если он не был опубликован ранее по этому же символу/бирже).
    /// </summary>
    /// <param name="exchange">Биржа.</param>
    /// <param name="symbol">Символ инструмента.</param>
    /// <param name="ts">Метка времени сделки на бирже.</param>
    /// <param name="tradeId">Внешний идентификатор сделки (если есть).</param>
    /// <param name="price">Цена сделки.</param>
    /// <param name="volume">Объём сделки.</param>
    /// <returns><c>true</c>, если тик стоит публиковать; иначе <c>false</c>.</returns>
    public bool ShouldEmit(MarketExchange exchange, string symbol, DateTimeOffset ts, string? tradeId, decimal price, decimal volume)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        var key = new TradeKey(exchange, symbol);
        var state = _stateByKey.GetOrAdd(key, _ => new TradeCursorState());
        var fingerprint = BuildFingerprint(exchange, symbol, ts, tradeId, price, volume);
        var now = DateTimeOffset.UtcNow;

        lock (state.Sync)
        {
            CleanupFingerprints(state, now);

            if (state.RecentFingerprints.ContainsKey(fingerprint))
                return false;

            if (string.IsNullOrWhiteSpace(tradeId) && ts < state.LastTimestamp)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Отмечает тик как опубликованный и обновляет курсор по бирже/символу.
    /// </summary>
    /// <param name="exchange">Биржа.</param>
    /// <param name="symbol">Символ инструмента.</param>
    /// <param name="ts">Метка времени сделки на бирже.</param>
    /// <param name="tradeId">Внешний идентификатор сделки (если есть).</param>
    /// <param name="price">Цена сделки.</param>
    /// <param name="volume">Объём сделки.</param>
    public void MarkEmitted(MarketExchange exchange, string symbol, DateTimeOffset ts, string? tradeId, decimal price, decimal volume)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        var key = new TradeKey(exchange, symbol);
        var state = _stateByKey.GetOrAdd(key, _ => new TradeCursorState());
        var fingerprint = BuildFingerprint(exchange, symbol, ts, tradeId, price, volume);
        var now = DateTimeOffset.UtcNow;

        lock (state.Sync)
        {
            CleanupFingerprints(state, now);

            if (ts > state.LastTimestamp)
                state.LastTimestamp = ts;

            state.RecentFingerprints[fingerprint] = now;
        }
    }

    private void CleanupFingerprints(TradeCursorState state, DateTimeOffset now)
    {
        if (state.RecentFingerprints.Count == 0)
            return;

        var expired = now - _fingerprintTtl;
        foreach (var pair in state.RecentFingerprints.Where(pair => pair.Value < expired).ToList())
            state.RecentFingerprints.Remove(pair.Key);
    }

    private static string BuildFingerprint(
        MarketExchange exchange,
        string symbol,
        DateTimeOffset ts,
        string? tradeId,
        decimal price,
        decimal volume)
    {
        var source = exchange.ToString();
        return TickFingerprint.Build(source, symbol, ts, price, volume, tradeId);
    }

    private readonly record struct TradeKey(MarketExchange Exchange, string Symbol);

    private sealed class TradeCursorState
    {
        public object Sync { get; } = new();
        public DateTimeOffset LastTimestamp { get; set; } = DateTimeOffset.MinValue;
        public Dictionary<string, DateTimeOffset> RecentFingerprints { get; } = new(StringComparer.Ordinal);
    }
}

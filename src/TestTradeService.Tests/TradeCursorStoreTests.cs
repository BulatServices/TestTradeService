using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты курсора сделок для дедупликации между источниками.
/// </summary>
public sealed class TradeCursorStoreTests
{
    /// <summary>
    /// Проверяет дедупликацию по TradeId: один и тот же TradeId не должен эмититься повторно,
    /// а новый TradeId должен проходить даже при более старом timestamp.
    /// </summary>
    [Fact]
    public void ShouldEmit_WithTradeId_DeduplicatesAndIgnoresTimestampOrdering()
    {
        var store = new TradeCursorStore();
        var exchange = MarketExchange.Coinbase;
        const string symbol = "BTC-USD";

        var ts1 = DateTimeOffset.FromUnixTimeMilliseconds(2_000);
        var ts2 = DateTimeOffset.FromUnixTimeMilliseconds(3_000);
        var tsOlder = DateTimeOffset.FromUnixTimeMilliseconds(1_000);

        Assert.True(store.ShouldEmit(exchange, symbol, ts1, "A", 1m, 1m));
        store.MarkEmitted(exchange, symbol, ts1, "A", 1m, 1m);

        Assert.True(store.ShouldEmit(exchange, symbol, ts2, "B", 1m, 1m));
        store.MarkEmitted(exchange, symbol, ts2, "B", 1m, 1m);

        Assert.False(store.ShouldEmit(exchange, symbol, ts2, "A", 999m, 999m));

        Assert.True(store.ShouldEmit(exchange, symbol, tsOlder, "C", 1m, 1m));
    }

    /// <summary>
    /// Проверяет fallback без TradeId: один и тот же fingerprint не должен эмититься повторно,
    /// а тик с более старым timestamp должен быть отброшен по правилу упорядочивания времени.
    /// </summary>
    [Fact]
    public void ShouldEmit_WithoutTradeId_DeduplicatesAndRejectsOlderTimestamp()
    {
        var store = new TradeCursorStore();
        var exchange = MarketExchange.Coinbase;
        const string symbol = "BTC-USD";

        var ts1 = DateTimeOffset.FromUnixTimeMilliseconds(2_000);
        var tsOlder = DateTimeOffset.FromUnixTimeMilliseconds(1_000);

        Assert.True(store.ShouldEmit(exchange, symbol, ts1, null, 1m, 1m));
        store.MarkEmitted(exchange, symbol, ts1, null, 1m, 1m);

        Assert.False(store.ShouldEmit(exchange, symbol, ts1, null, 1m, 1m));
        Assert.False(store.ShouldEmit(exchange, symbol, tsOlder, null, 2m, 2m));
    }
}


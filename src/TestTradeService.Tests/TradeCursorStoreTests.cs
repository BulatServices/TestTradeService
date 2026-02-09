using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты in-memory курсора сделок для дедупликации публикаций.
/// </summary>
public sealed class TradeCursorStoreTests
{
    /// <summary>
    /// Проверяет, что тик с более старым временем после уже опубликованного тика не публикуется повторно.
    /// </summary>
    [Fact]
    public void ShouldEmit_WhenTradeHasOlderTimestamp_ReturnsFalse()
    {
        var store = new TradeCursorStore();
        var exchange = MarketExchange.Bybit;
        const string symbol = "BTCUSDT";
        var newerTimestamp = DateTimeOffset.UtcNow;
        var olderTimestamp = newerTimestamp.AddSeconds(-10);

        Assert.True(store.ShouldEmit(exchange, symbol, newerTimestamp, "new-1", 100m, 1m));
        store.MarkEmitted(exchange, symbol, newerTimestamp, "new-1", 100m, 1m);

        var shouldEmitOlder = store.ShouldEmit(exchange, symbol, olderTimestamp, "old-1", 99m, 0.5m);

        Assert.False(shouldEmitOlder);
    }

    /// <summary>
    /// Проверяет, что для одной и той же метки времени допускаются разные сделки с разными идентификаторами.
    /// </summary>
    [Fact]
    public void ShouldEmit_WhenTradeHasSameTimestampButDifferentTradeId_ReturnsTrue()
    {
        var store = new TradeCursorStore();
        var exchange = MarketExchange.Bybit;
        const string symbol = "BTCUSDT";
        var timestamp = DateTimeOffset.UtcNow;

        Assert.True(store.ShouldEmit(exchange, symbol, timestamp, "trade-1", 100m, 1m));
        store.MarkEmitted(exchange, symbol, timestamp, "trade-1", 100m, 1m);

        var shouldEmitSecondTrade = store.ShouldEmit(exchange, symbol, timestamp, "trade-2", 101m, 0.8m);

        Assert.True(shouldEmitSecondTrade);
    }
}

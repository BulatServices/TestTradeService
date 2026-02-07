using TestTradeService.Services.Exchanges.Coinbase;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты парсинга payload сделок Coinbase Exchange.
/// </summary>
public sealed class CoinbaseTradesParsingTests
{
    /// <summary>
    /// Проверяет, что WebSocket payload типа <c>match</c> парсится в тик.
    /// </summary>
    [Fact]
    public void ParseWebSocketMatches_MatchMessage_ReturnsTick()
    {
        const string payload =
            "{\"type\":\"match\",\"trade_id\":123,\"product_id\":\"BTC-USD\",\"price\":\"50000.10\",\"size\":\"0.001\",\"time\":\"2020-01-01T00:00:00Z\"}";

        var ticks = CoinbaseTradesParsing.ParseWebSocketMatches(payload, DateTimeOffset.UtcNow);
        var tick = Assert.Single(ticks);

        Assert.Equal("BTC-USD", tick.Symbol);
        Assert.Equal("123", tick.TradeId);
    }

    /// <summary>
    /// Проверяет, что служебное WebSocket-сообщение не даёт тиков.
    /// </summary>
    [Fact]
    public void ParseWebSocketMatches_NonMatchMessage_ReturnsEmpty()
    {
        const string payload = "{\"type\":\"subscriptions\",\"channels\":[]}";

        var ticks = CoinbaseTradesParsing.ParseWebSocketMatches(payload, DateTimeOffset.UtcNow);

        Assert.Empty(ticks);
    }

    /// <summary>
    /// Проверяет, что REST payload trades парсится в список тиков.
    /// </summary>
    [Fact]
    public void ParseRestTrades_ReturnsTicks()
    {
        const string payload =
            "[{\"trade_id\":123,\"price\":\"50000.10\",\"size\":\"0.001\",\"time\":\"2020-01-01T00:00:00Z\"},{\"trade_id\":122,\"price\":\"49900.00\",\"size\":\"0.002\",\"time\":\"2019-12-31T23:59:59Z\"}]";

        var ticks = CoinbaseTradesParsing.ParseRestTrades("BTC-USD", payload);

        Assert.Equal(2, ticks.Count);
        Assert.All(ticks, t => Assert.Equal("BTC-USD", t.Symbol));
    }
}

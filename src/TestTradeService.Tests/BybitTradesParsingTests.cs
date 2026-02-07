using TestTradeService.Services.Exchanges.Bybit;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты парсинга payload сделок Bybit.
/// </summary>
public sealed class BybitTradesParsingTests
{
    /// <summary>
    /// Проверяет, что WebSocket payload Bybit publicTrade парсится в тик.
    /// </summary>
    [Fact]
    public void ParseWebSocketPublicTrades_ReturnsTicks()
    {
        const string payload =
            "{\"topic\":\"publicTrade.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":[{\"T\":1700000000000,\"s\":\"BTCUSDT\",\"p\":\"50000.1\",\"v\":\"0.001\",\"i\":\"abc\"}]}";

        var ticks = BybitTradesParsing.ParseWebSocketPublicTrades(payload, DateTimeOffset.UtcNow);

        Assert.Single(ticks);
        Assert.Equal("BTCUSDT", ticks[0].Symbol);
        Assert.Equal("abc", ticks[0].TradeId);
    }

    /// <summary>
    /// Проверяет, что служебное сообщение Bybit без <c>publicTrade</c> не даёт тиков.
    /// </summary>
    [Fact]
    public void ParseWebSocketPublicTrades_NonTrade_ReturnsEmpty()
    {
        const string payload = "{\"op\":\"subscribe\",\"success\":true}";

        var ticks = BybitTradesParsing.ParseWebSocketPublicTrades(payload, DateTimeOffset.UtcNow);

        Assert.Empty(ticks);
    }

    /// <summary>
    /// Проверяет, что REST payload Bybit recent-trade парсится в тик.
    /// </summary>
    [Fact]
    public void ParseRestRecentTrades_ReturnsTicks()
    {
        const string payload =
            "{\"retCode\":0,\"retMsg\":\"OK\",\"result\":{\"category\":\"spot\",\"list\":[{\"execId\":\"abc\",\"price\":\"50000.1\",\"size\":\"0.001\",\"time\":\"1700000000000\"}]}}";

        var ticks = BybitTradesParsing.ParseRestRecentTrades("BTCUSDT", payload);

        Assert.Single(ticks);
        Assert.Equal("BTCUSDT", ticks[0].Symbol);
        Assert.Equal("abc", ticks[0].TradeId);
    }
}


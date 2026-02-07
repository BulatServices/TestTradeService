using TestTradeService.Services.Exchanges.Kraken;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты парсинга payload сделок Kraken.
/// </summary>
public sealed class KrakenTradesParsingTests
{
    /// <summary>
    /// Проверяет, что WebSocket payload Kraken с массивом сделок преобразуется в набор тиков.
    /// </summary>
    [Fact]
    public void ParseWebSocketTrades_ReturnsTicks()
    {
        const string payload =
            "[42, [[\"50000.1\",\"0.002\",\"1700000000.123\",\"b\",\"l\",\"\"],[\"50000.2\",\"0.001\",\"1700000000.124\",\"s\",\"l\",\"\"]], \"trade\", \"XBT/USD\"]";

        var ticks = KrakenTradesParsing.ParseWebSocketTrades(payload, DateTimeOffset.UtcNow);

        Assert.Equal(2, ticks.Count);
        Assert.All(ticks, t => Assert.Equal("XBT/USD", t.Symbol));
    }

    /// <summary>
    /// Проверяет, что служебное WebSocket-сообщение Kraken не даёт тиков.
    /// </summary>
    [Fact]
    public void ParseWebSocketTrades_NonTradeMessage_ReturnsEmpty()
    {
        const string payload = "{\"event\":\"heartbeat\"}";

        var ticks = KrakenTradesParsing.ParseWebSocketTrades(payload, DateTimeOffset.UtcNow);

        Assert.Empty(ticks);
    }

    /// <summary>
    /// Проверяет, что REST payload Kraken Trades парсится в тики и возвращает курсор <c>last</c>.
    /// </summary>
    [Fact]
    public void ParseRestTrades_ReturnsTicksAndCursor()
    {
        const string payload =
            "{\"error\":[],\"result\":{\"XBTUSD\":[[\"50000.1\",\"0.002\",\"1700000000.123\",\"b\",\"l\",\"\"],[\"50000.2\",\"0.001\",\"1700000000.124\",\"s\",\"l\",\"\"]],\"last\":\"1700000000123\"}}";

        var ticks = KrakenTradesParsing.ParseRestTrades("XBT/USD", payload, out var last);

        Assert.Equal(2, ticks.Count);
        Assert.Equal("1700000000123", last);
    }
}


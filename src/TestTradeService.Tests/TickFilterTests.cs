using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты фильтрации тиков.
/// </summary>
public sealed class TickFilterTests
{
    /// <summary>
    /// Проверяет, что тик с Price &lt;= 0 отбрасывается даже при разрешённом символе.
    /// </summary>
    [Fact]
    public void IsAllowed_PriceIsZero_ReturnsFalse()
    {
        var filter = new TickFilter(new[] { "BTC-USD" });

        var tick = new NormalizedTick
        {
            Source = "Coinbase",
            Symbol = "BTC-USD",
            Price = 0m,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "x"
        };

        Assert.False(filter.IsAllowed(tick));
    }

    /// <summary>
    /// Проверяет, что тик с Volume &lt;= 0 отбрасывается даже при разрешённом символе.
    /// </summary>
    [Fact]
    public void IsAllowed_VolumeIsZero_ReturnsFalse()
    {
        var filter = new TickFilter(new[] { "BTC-USD" });

        var tick = new NormalizedTick
        {
            Source = "Coinbase",
            Symbol = "BTC-USD",
            Price = 1m,
            Volume = 0m,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "x"
        };

        Assert.False(filter.IsAllowed(tick));
    }

    /// <summary>
    /// Проверяет, что корректный тик с положительными Price/Volume и разрешённым символом пропускается.
    /// </summary>
    [Fact]
    public void IsAllowed_ValidTick_ReturnsTrue()
    {
        var filter = new TickFilter(new[] { "BTC-USD" });

        var tick = new NormalizedTick
        {
            Source = "Coinbase",
            Symbol = "BTC-USD",
            Price = 1m,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "x"
        };

        Assert.True(filter.IsAllowed(tick));
    }
}


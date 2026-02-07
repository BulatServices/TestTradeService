using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты правила порогов цены.
/// </summary>
public sealed class PriceThresholdRuleTests
{
    /// <summary>
    /// Проверяет пороговые условия по цене.
    /// </summary>
    [Theory]
    [InlineData(17_999, true)]
    [InlineData(18_000, false)]
    [InlineData(21_999, false)]
    [InlineData(22_001, true)]
    public void IsMatch_WhenPriceOutsideRange_ReturnsExpected(decimal price, bool expected)
    {
        var rule = new PriceThresholdRule();
        var tick = CreateTick(price);
        var metrics = CreateMetrics();

        var match = rule.IsMatch(tick, metrics);

        Assert.Equal(expected, match);
    }

    /// <summary>
    /// Проверяет формирование алерта с заполненными полями.
    /// </summary>
    [Fact]
    public void CreateAlert_SetsExpectedFields()
    {
        var rule = new PriceThresholdRule();
        var tick = CreateTick(23_000m);
        var metrics = CreateMetrics();

        var alert = rule.CreateAlert(tick, metrics);

        Assert.Equal(rule.Name, alert.Rule);
        Assert.Equal(tick.Source, alert.Source);
        Assert.Equal(tick.Symbol, alert.Symbol);
        Assert.Equal(tick.Timestamp, alert.Timestamp);
        Assert.Contains("Price threshold", alert.Message);
    }

    private static NormalizedTick CreateTick(decimal price)
    {
        return new NormalizedTick
        {
            Source = "Kraken",
            Symbol = "BTCUSD",
            Price = price,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "fp"
        };
    }

    private static MetricsSnapshot CreateMetrics()
    {
        return new MetricsSnapshot
        {
            Symbol = "BTCUSD",
            WindowStart = DateTimeOffset.UtcNow,
            Window = TimeSpan.FromMinutes(1),
            AveragePrice = 20_000m,
            Volatility = 1m,
            Count = 1
        };
    }
}

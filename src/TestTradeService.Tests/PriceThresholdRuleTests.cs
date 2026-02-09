using TestTradeService.Models;
using TestTradeService.Services;
using TestTradeService.Storage;
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
    [InlineData(9_999, true)]
    [InlineData(10_000, false)]
    [InlineData(199_999, false)]
    [InlineData(200_001, true)]
    public void IsMatch_WhenPriceOutsideRange_ReturnsExpected(decimal price, bool expected)
    {
        var rule = new PriceThresholdRule(new AlertRuleConfigProvider(DefaultConfigurationFactory.CreateAlertRules()));
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
        var rule = new PriceThresholdRule(new AlertRuleConfigProvider(DefaultConfigurationFactory.CreateAlertRules()));
        var tick = CreateTick(23_000m);
        var metrics = CreateMetrics();

        var alert = rule.CreateAlert(tick, metrics);

        Assert.Equal(rule.Name, alert.Rule);
        Assert.Equal(tick.Source, alert.Source);
        Assert.Equal(tick.Symbol, alert.Symbol);
        Assert.Equal(tick.Timestamp, alert.Timestamp);
        Assert.Contains("Price threshold", alert.Message);
    }

    /// <summary>
    /// Проверяет, что при отсутствии конфигурации по тикеру правило не срабатывает.
    /// </summary>
    [Fact]
    public void IsMatch_WhenTickerHasNoConfiguredThreshold_ReturnsFalse()
    {
        var provider = new AlertRuleConfigProvider(new[]
        {
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = "Kraken",
                Symbol = "XBT/USD",
                Parameters = new Dictionary<string, string>
                {
                    ["min_price"] = "18000",
                    ["max_price"] = "22000"
                }
            }
        });
        var rule = new PriceThresholdRule(provider);
        var tick = new NormalizedTick
        {
            Source = "Coinbase-WebSocket",
            Symbol = "BTC-USD",
            Price = 25_000m,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "fp-missing"
        };

        var match = rule.IsMatch(tick, CreateMetrics());

        Assert.False(match);
    }

    private static NormalizedTick CreateTick(decimal price)
    {
        return new NormalizedTick
        {
            Source = "Kraken-WebSocket",
            Symbol = "XBT/USD",
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
            Count = 1,
            AverageVolume = 1m
        };
    }
}

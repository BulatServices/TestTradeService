using TestTradeService.Models;
using TestTradeService.Services;
using TestTradeService.Storage;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты правила всплеска объема.
/// </summary>
public sealed class VolumeSpikeRuleTests
{
    /// <summary>
    /// Проверяет условия срабатывания правила по объему, числу тиков и относительному коэффициенту.
    /// </summary>
    [Theory]
    [InlineData(4.1, 6, 2.0, true)]
    [InlineData(4.0, 6, 2.0, false)]
    [InlineData(5.0, 5, 2.0, false)]
    [InlineData(3.5, 7, 2.0, false)]
    [InlineData(4.1, 6, 3.0, false)]
    public void IsMatch_WhenVolumeAndCountMeetThresholds_ReturnsExpected(decimal volume, int count, decimal averageVolume, bool expected)
    {
        var rule = new VolumeSpikeRule(new AlertRuleConfigProvider(DefaultConfigurationFactory.CreateAlertRules()));
        var tick = CreateTick(volume);
        var metrics = CreateMetrics(count, averageVolume);

        var match = rule.IsMatch(tick, metrics);

        Assert.Equal(expected, match);
    }

    /// <summary>
    /// Проверяет, что правило не срабатывает при нулевом baseline объема.
    /// </summary>
    [Fact]
    public void IsMatch_WhenAverageVolumeIsZero_ReturnsFalse()
    {
        var rule = new VolumeSpikeRule(new AlertRuleConfigProvider(DefaultConfigurationFactory.CreateAlertRules()));

        var match = rule.IsMatch(CreateTick(10m), CreateMetrics(10, 0m));

        Assert.False(match);
    }

    /// <summary>
    /// Проверяет формирование алерта по всплеску объема.
    /// </summary>
    [Fact]
    public void CreateAlert_SetsExpectedFields()
    {
        var rule = new VolumeSpikeRule(new AlertRuleConfigProvider(DefaultConfigurationFactory.CreateAlertRules()));
        var tick = CreateTick(5m);
        var metrics = CreateMetrics(10, 2m);

        var alert = rule.CreateAlert(tick, metrics);

        Assert.Equal(rule.Name, alert.Rule);
        Assert.Equal(tick.Source, alert.Source);
        Assert.Equal(tick.Symbol, alert.Symbol);
        Assert.Equal(tick.Timestamp, alert.Timestamp);
        Assert.Contains("Volume spike", alert.Message);
    }

    private static NormalizedTick CreateTick(decimal volume)
    {
        return new NormalizedTick
        {
            Source = "Bybit",
            Symbol = "BTCUSDT",
            Price = 20_000m,
            Volume = volume,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "fp"
        };
    }

    private static MetricsSnapshot CreateMetrics(int count, decimal averageVolume)
    {
        return new MetricsSnapshot
        {
            Symbol = "BTCUSDT",
            WindowStart = DateTimeOffset.UtcNow,
            Window = TimeSpan.FromMinutes(1),
            AveragePrice = 20_000m,
            Volatility = 0.2m,
            Count = count,
            AverageVolume = averageVolume
        };
    }
}

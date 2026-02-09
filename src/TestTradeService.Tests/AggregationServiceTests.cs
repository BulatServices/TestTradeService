using System.Linq;
using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты сервиса агрегации тиков в свечи и метрики.
/// </summary>
public sealed class AggregationServiceTests
{
    /// <summary>
    /// Проверяет закрытие минутной свечи при переходе через границу окна.
    /// </summary>
    [Fact]
    public void Update_WhenWindowRolls_EmitsCandleWithExpectedValues()
    {
        var service = new AggregationService();
        var baseTime = new DateTimeOffset(2024, 01, 01, 12, 00, 10, TimeSpan.Zero);

        var tick1 = CreateTick(baseTime, 100m, 1m);
        var tick2 = CreateTick(baseTime.AddSeconds(40), 110m, 2m);
        var tick3 = CreateTick(baseTime.AddMinutes(1).AddSeconds(5), 90m, 3m);

        Assert.Empty(service.Update(tick1));
        Assert.Empty(service.Update(tick2));

        var candles = service.Update(tick3).ToList();

        Assert.Single(candles);
        var candle = candles[0];
        Assert.Equal(TimeSpan.FromMinutes(1), candle.Window);
        Assert.Equal(new DateTimeOffset(2024, 01, 01, 12, 00, 00, TimeSpan.Zero), candle.WindowStart);
        Assert.Equal(100m, candle.Open);
        Assert.Equal(110m, candle.High);
        Assert.Equal(100m, candle.Low);
        Assert.Equal(110m, candle.Close);
        Assert.Equal(3m, candle.Volume);
        Assert.Equal(2, candle.Count);
    }

    /// <summary>
    /// Проверяет расчет скользящих метрик и очистку окна по времени.
    /// </summary>
    [Fact]
    public void UpdateMetrics_WhenTicksSlide_ReturnsExpectedSnapshot()
    {
        var service = new AggregationService();
        var start = new DateTimeOffset(2024, 01, 01, 12, 00, 00, TimeSpan.Zero);

        var tick1 = CreateTick(start, 10m, 1m);
        var tick2 = CreateTick(start.AddSeconds(30), 20m, 1m);
        var tick3 = CreateTick(start.AddSeconds(90), 30m, 1m);

        service.UpdateMetrics(tick1);
        var snapshot = service.UpdateMetrics(tick2);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(15m, snapshot.AveragePrice);
        Assert.Equal(5m, snapshot.Volatility);
        Assert.Equal(1m, snapshot.AverageVolume);

        snapshot = service.UpdateMetrics(tick3);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(25m, snapshot.AveragePrice);
        Assert.Equal(5m, snapshot.Volatility);
        Assert.Equal(1m, snapshot.AverageVolume);
    }

    private static NormalizedTick CreateTick(DateTimeOffset timestamp, decimal price, decimal volume)
    {
        return new NormalizedTick
        {
            Source = "Binance",
            Symbol = "BTCUSDT",
            Price = price,
            Volume = volume,
            Timestamp = timestamp,
            Fingerprint = $"fp-{timestamp.ToUnixTimeMilliseconds()}-{price}-{volume}"
        };
    }
}

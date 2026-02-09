using TestTradeService.Monitoring;
using TestTradeService.Monitoring.Configuration;
using TestTradeService.Models;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты сервиса мониторинга.
/// </summary>
public sealed class MonitoringServiceTests
{
    /// <summary>
    /// Проверяет запись статистики по источнику и бирже, включая rolling-метрики.
    /// </summary>
    [Fact]
    public void RecordTickAndAggregate_UpdatesSnapshotStats()
    {
        var service = new MonitoringService(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromSeconds(2),
            RollingWindow = TimeSpan.FromMinutes(5)
        });
        var tick = CreateTick("Bybit-spot", DateTimeOffset.UtcNow);
        var candle = CreateCandle(tick);

        service.RecordTick("Bybit-spot", tick);
        service.RecordAggregate("Bybit-spot", candle);
        service.RecordDelay("Bybit-spot", TimeSpan.FromMilliseconds(500));

        var snapshot = service.Snapshot();

        Assert.Single(snapshot.SourceStats);
        Assert.Single(snapshot.ExchangeStats);

        var sourceStats = snapshot.SourceStats["Bybit-spot"];
        Assert.Equal(1, sourceStats.TickCount);
        Assert.Equal(1, sourceStats.AggregateCount);
        Assert.True(sourceStats.AverageDelayMs > 0);
        Assert.True(sourceStats.WindowTickCount >= 1);
        Assert.True(sourceStats.WindowAggregateCount >= 1);
        Assert.True(sourceStats.WindowAvgDelayMs > 0);
        Assert.True(sourceStats.WindowMaxDelayMs > 0);

        var exchangeStats = snapshot.ExchangeStats[MarketExchange.Bybit];
        Assert.Equal(1, exchangeStats.TickCount);
        Assert.Equal(1, exchangeStats.AggregateCount);
        Assert.True(exchangeStats.AverageDelayMs > 0);
        Assert.True(exchangeStats.WindowTickCount >= 1);
        Assert.True(exchangeStats.WindowAggregateCount >= 1);

        Assert.True(snapshot.PerformanceReport.TotalWindowTickCount >= 1);
        Assert.True(snapshot.PerformanceReport.TotalWindowAggregateCount >= 1);
    }

    /// <summary>
    /// Проверяет переход источника в статус Warn при превышении порога 2x SLA.
    /// </summary>
    [Fact]
    public void RecordDelay_WhenAboveWarnThreshold_SetsWarnStatus()
    {
        var service = new MonitoringService(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromMilliseconds(100),
            WarnMultiplier = 2,
            CriticalMultiplier = 4,
            RollingWindow = TimeSpan.FromMinutes(5)
        });

        service.RecordTick("Kraken-spot", CreateTick("Kraken-spot", DateTimeOffset.UtcNow));
        service.RecordDelay("Kraken-spot", TimeSpan.FromMilliseconds(250));

        var snapshot = service.Snapshot();
        var source = snapshot.SourceStats["Kraken-spot"];

        Assert.Equal(MonitoringSourceStatus.Warn, source.Status);
        Assert.Single(snapshot.Warnings);
        Assert.Contains("Warn", snapshot.Warnings[0]);
    }

    /// <summary>
    /// Проверяет переход источника в статус Critical при превышении порога 4x SLA.
    /// </summary>
    [Fact]
    public void RecordDelay_WhenAboveCriticalThreshold_SetsCriticalStatus()
    {
        var service = new MonitoringService(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromMilliseconds(100),
            WarnMultiplier = 2,
            CriticalMultiplier = 4,
            RollingWindow = TimeSpan.FromMinutes(5)
        });

        service.RecordTick("Coinbase-spot", CreateTick("Coinbase-spot", DateTimeOffset.UtcNow));
        service.RecordDelay("Coinbase-spot", TimeSpan.FromMilliseconds(450));

        var snapshot = service.Snapshot();
        var source = snapshot.SourceStats["Coinbase-spot"];

        Assert.Equal(MonitoringSourceStatus.Critical, source.Status);
        Assert.Single(snapshot.Warnings);
        Assert.Contains("Critical", snapshot.Warnings[0]);
    }

    /// <summary>
    /// Проверяет переход источника в статус Critical при устаревшем времени последнего тика.
    /// </summary>
    [Fact]
    public void RecordTick_WhenLastTickAgeExceeded_SetsCriticalStatus()
    {
        var service = new MonitoringService(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromMilliseconds(100),
            WarnMultiplier = 2,
            CriticalMultiplier = 4,
            RollingWindow = TimeSpan.FromMinutes(5)
        });

        service.RecordTick("unknown", CreateTick("unknown", DateTimeOffset.UtcNow.AddSeconds(-2)));

        var snapshot = service.Snapshot();

        Assert.True(snapshot.ExchangeStats.ContainsKey(MarketExchange.Unknown));
        Assert.Equal(MonitoringSourceStatus.Critical, snapshot.SourceStats["unknown"].Status);
    }

    /// <summary>
    /// Проверяет, что предупреждения вычисляются из текущего состояния и не накапливаются между снимками.
    /// </summary>
    [Fact]
    public void Snapshot_WhenCalledRepeatedly_DoesNotAccumulateWarnings()
    {
        var service = new MonitoringService(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromMilliseconds(100),
            WarnMultiplier = 2,
            CriticalMultiplier = 4,
            RollingWindow = TimeSpan.FromMinutes(5)
        });

        service.RecordTick("Bybit-spot", CreateTick("Bybit-spot", DateTimeOffset.UtcNow));
        service.RecordDelay("Bybit-spot", TimeSpan.FromMilliseconds(450));

        var first = service.Snapshot();
        var second = service.Snapshot();

        Assert.Single(first.Warnings);
        Assert.Single(second.Warnings);
    }

    private static NormalizedTick CreateTick(string source, DateTimeOffset timestamp)
    {
        return new NormalizedTick
        {
            Source = source,
            Symbol = "BTCUSDT",
            Price = 10m,
            Volume = 1m,
            Timestamp = timestamp,
            Fingerprint = "fp"
        };
    }

    private static AggregatedCandle CreateCandle(NormalizedTick tick)
    {
        return new AggregatedCandle
        {
            Source = tick.Source,
            Symbol = tick.Symbol,
            WindowStart = tick.Timestamp,
            Window = TimeSpan.FromMinutes(1),
            Open = tick.Price,
            High = tick.Price,
            Low = tick.Price,
            Close = tick.Price,
            Volume = tick.Volume,
            Count = 1
        };
    }
}

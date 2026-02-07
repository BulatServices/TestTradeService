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
    /// Проверяет запись статистики по источнику и бирже.
    /// </summary>
    [Fact]
    public void RecordTickAndAggregate_UpdatesSnapshotStats()
    {
        var service = new MonitoringService(new MonitoringSlaConfig { MaxTickDelay = TimeSpan.FromSeconds(2) });
        var tick = CreateTick("Binance-spot", new DateTimeOffset(2024, 01, 01, 12, 00, 00, TimeSpan.Zero));
        var candle = CreateCandle(tick);

        service.RecordTick("Binance-spot", tick);
        service.RecordAggregate("Binance-spot", candle);
        service.RecordDelay("Binance-spot", TimeSpan.FromMilliseconds(500));

        var snapshot = service.Snapshot();

        Assert.Single(snapshot.SourceStats);
        Assert.Single(snapshot.ExchangeStats);

        var sourceStats = snapshot.SourceStats["Binance-spot"];
        Assert.Equal(1, sourceStats.TickCount);
        Assert.Equal(1, sourceStats.AggregateCount);
        Assert.True(sourceStats.AverageDelayMs > 0);
        Assert.Equal(tick.Timestamp, sourceStats.LastTickTime);

        var exchangeStats = snapshot.ExchangeStats[MarketExchange.Binance];
        Assert.Equal(1, exchangeStats.TickCount);
        Assert.Equal(1, exchangeStats.AggregateCount);
        Assert.True(exchangeStats.AverageDelayMs > 0);
        Assert.Equal(tick.Timestamp, exchangeStats.LastTickTime);
    }

    /// <summary>
    /// Проверяет добавление предупреждения при превышении SLA по задержке.
    /// </summary>
    [Fact]
    public void RecordDelay_WhenAboveSla_AddsWarning()
    {
        var service = new MonitoringService(new MonitoringSlaConfig { MaxTickDelay = TimeSpan.FromMilliseconds(100) });

        service.RecordDelay("Kraken-spot", TimeSpan.FromMilliseconds(250));

        var snapshot = service.Snapshot();

        Assert.Single(snapshot.Warnings);
        Assert.Contains("Kraken", snapshot.Warnings[0]);
    }

    /// <summary>
    /// Проверяет, что неизвестный источник помечается как Unknown.
    /// </summary>
    [Fact]
    public void RecordTick_WhenSourceUnknown_UsesUnknownExchange()
    {
        var service = new MonitoringService(new MonitoringSlaConfig());
        var tick = CreateTick("unknown", DateTimeOffset.UtcNow);

        service.RecordTick("unknown", tick);

        var snapshot = service.Snapshot();

        Assert.True(snapshot.ExchangeStats.ContainsKey(MarketExchange.Unknown));
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

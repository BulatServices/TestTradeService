using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты конвейера обработки данных.
/// </summary>
public sealed class DataPipelineTests
{
    /// <summary>
    /// Проверяет полный проход тика через конвейер и сохранение результатов.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenTickPassesThrough_StoresAndAlerts()
    {
        var aggregation = new CapturingAggregationService();
        var storage = new CapturingStorage();
        var alertingStorage = new CapturingStorage();
        var alerting = new AlertingService(
            new[] { new AlwaysMatchRule() },
            new INotifier[] { new CapturingNotifier() },
            alertingStorage,
            NullLogger<AlertingService>.Instance);
        var monitoring = new CapturingMonitoringService();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Symbols = new[] { "BTCUSDT" }
                }
            }
        };
        var pipeline = new DataPipeline(
            aggregation,
            storage,
            alerting,
            monitoring,
            config,
            NullLogger<DataPipeline>.Instance);

        var channel = Channel.CreateUnbounded<Tick>();
        var tick = new Tick
        {
            Source = "Binance",
            Symbol = "BTCUSDT",
            Price = 20_000m,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            TradeId = "1"
        };

        await channel.Writer.WriteAsync(tick);
        channel.Writer.Complete();

        await pipeline.StartAsync(channel.Reader, CancellationToken.None);

        Assert.Single(storage.StoredTicks);
        Assert.Single(storage.StoredAggregates);
        Assert.Single(alertingStorage.StoredAlerts);
        Assert.Equal(1, monitoring.DelayCalls);
        Assert.Equal(1, monitoring.TickCalls);
        Assert.Equal(1, monitoring.AggregateCalls);
    }

    /// <summary>
    /// Проверяет фильтрацию тиков по списку символов.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenSymbolNotAllowed_SkipsProcessing()
    {
        var aggregation = new CapturingAggregationService();
        var storage = new CapturingStorage();
        var alertingStorage = new CapturingStorage();
        var alerting = new AlertingService(
            new[] { new AlwaysMatchRule() },
            new INotifier[] { new CapturingNotifier() },
            alertingStorage,
            NullLogger<AlertingService>.Instance);
        var monitoring = new CapturingMonitoringService();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Symbols = new[] { "BTCUSDT" }
                }
            }
        };
        var pipeline = new DataPipeline(
            aggregation,
            storage,
            alerting,
            monitoring,
            config,
            NullLogger<DataPipeline>.Instance);

        var channel = Channel.CreateUnbounded<Tick>();
        var tick = new Tick
        {
            Source = "Binance",
            Symbol = "ETHUSDT",
            Price = 20_000m,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            TradeId = "1"
        };

        await channel.Writer.WriteAsync(tick);
        channel.Writer.Complete();

        await pipeline.StartAsync(channel.Reader, CancellationToken.None);

        Assert.Empty(storage.StoredTicks);
        Assert.Empty(storage.StoredAggregates);
        Assert.Empty(alertingStorage.StoredAlerts);
        Assert.Equal(0, monitoring.TickCalls);
    }

    /// <summary>
    /// Проверяет отбрасывание дубликатов по fingerprint.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenDuplicateTick_SkipsSecond()
    {
        var aggregation = new CapturingAggregationService();
        var storage = new CapturingStorage();
        var alertingStorage = new CapturingStorage();
        var alerting = new AlertingService(
            new[] { new AlwaysMatchRule() },
            new INotifier[] { new CapturingNotifier() },
            alertingStorage,
            NullLogger<AlertingService>.Instance);
        var monitoring = new CapturingMonitoringService();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Symbols = new[] { "BTCUSDT" }
                }
            }
        };
        var pipeline = new DataPipeline(
            aggregation,
            storage,
            alerting,
            monitoring,
            config,
            NullLogger<DataPipeline>.Instance);

        var channel = Channel.CreateUnbounded<Tick>();
        var timestamp = DateTimeOffset.UtcNow;
        var tick1 = new Tick
        {
            Source = "Binance",
            Symbol = "BTCUSDT",
            Price = 20_000m,
            Volume = 1m,
            Timestamp = timestamp,
            TradeId = "same"
        };
        var tick2 = tick1 with { Price = 20_001m };

        await channel.Writer.WriteAsync(tick1);
        await channel.Writer.WriteAsync(tick2);
        channel.Writer.Complete();

        await pipeline.StartAsync(channel.Reader, CancellationToken.None);

        Assert.Single(storage.StoredTicks);
    }

    private sealed class CapturingAggregationService : IAggregationService
    {
        public List<NormalizedTick> MetricsTicks { get; } = new();

        public IEnumerable<AggregatedCandle> Update(NormalizedTick tick)
        {
            return new[]
            {
                new AggregatedCandle
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
                }
            };
        }

        public MetricsSnapshot UpdateMetrics(NormalizedTick tick)
        {
            MetricsTicks.Add(tick);
            return new MetricsSnapshot
            {
                Symbol = tick.Symbol,
                WindowStart = tick.Timestamp.AddMinutes(-1),
                Window = TimeSpan.FromMinutes(1),
                AveragePrice = tick.Price,
                Volatility = 0,
                Count = 1
            };
        }
    }

    private sealed class CapturingStorage : IStorage
    {
        public List<NormalizedTick> StoredTicks { get; } = new();
        public List<AggregatedCandle> StoredAggregates { get; } = new();
        public List<Alert> StoredAlerts { get; } = new();

        public Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
        {
            StoredTicks.Add(tick);
            return Task.CompletedTask;
        }

        public Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
        {
            StoredAggregates.Add(candle);
            return Task.CompletedTask;
        }

        public Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken)
        {
            StoredAlerts.Add(alert);
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysMatchRule : IAlertRule
    {
        public string Name => "AlwaysMatch";

        public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics) => true;

        public Alert CreateAlert(NormalizedTick tick, MetricsSnapshot metrics)
        {
            return new Alert
            {
                Rule = Name,
                Source = tick.Source,
                Symbol = tick.Symbol,
                Message = "Alert",
                Timestamp = tick.Timestamp
            };
        }
    }

    private sealed class CapturingNotifier : INotifier
    {
        public string Name => "capture";

        public Task NotifyAsync(Alert alert, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingMonitoringService : IMonitoringService
    {
        public int DelayCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int AggregateCalls { get; private set; }

        public void RecordTick(string sourceName, NormalizedTick tick) => TickCalls++;

        public void RecordAggregate(string sourceName, AggregatedCandle candle) => AggregateCalls++;

        public void RecordDelay(string sourceName, TimeSpan delay) => DelayCalls++;

        public MonitoringSnapshot Snapshot() => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            ExchangeStats = new Dictionary<MarketExchange, ExchangeStats>(),
            SourceStats = new Dictionary<string, SourceStats>(),
            Warnings = Array.Empty<string>()
        };
    }
}

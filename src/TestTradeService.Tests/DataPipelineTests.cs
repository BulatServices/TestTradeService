using System.Threading.Channels;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Services;
using TestTradeService.Storage;
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
            new AlertRuleConfigProvider(Array.Empty<AlertRuleConfig>()),
            alertingStorage,
            NullLogger<AlertingService>.Instance);
        var monitoring = new CapturingMonitoringService();
        var eventBus = new CapturingMarketDataEventBus();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "BTCUSDT" }
                }
            }
        };
        var pipeline = new DataPipeline(
            aggregation,
            storage,
            alerting,
            monitoring,
            eventBus,
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

        Assert.Single(storage.StoredRawTicks);
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
            new AlertRuleConfigProvider(Array.Empty<AlertRuleConfig>()),
            alertingStorage,
            NullLogger<AlertingService>.Instance);
        var monitoring = new CapturingMonitoringService();
        var eventBus = new CapturingMarketDataEventBus();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "BTCUSDT" }
                }
            }
        };
        var pipeline = new DataPipeline(
            aggregation,
            storage,
            alerting,
            monitoring,
            eventBus,
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
            new AlertRuleConfigProvider(Array.Empty<AlertRuleConfig>()),
            alertingStorage,
            NullLogger<AlertingService>.Instance);
        var monitoring = new CapturingMonitoringService();
        var eventBus = new CapturingMarketDataEventBus();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "BTCUSDT" }
                }
            }
        };
        var pipeline = new DataPipeline(
            aggregation,
            storage,
            alerting,
            monitoring,
            eventBus,
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

    /// <summary>
    /// Проверяет, что порядок обработки сохраняется для каждого символа при нескольких партициях.
    /// </summary>
    [Fact]
    public async Task StartAsync_WithMultiplePartitions_PreservesPerSymbolOrder()
    {
        var aggregation = new CapturingAggregationService();
        var storage = new CapturingStorage();
        var monitoring = new CapturingMonitoringService();
        var eventBus = new CapturingMarketDataEventBus();
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "BTCUSDT", "ETHUSDT" }
                }
            }
        };

        var pipeline = new DataPipeline(
            aggregation,
            storage,
            new NoOpAlertingService(),
            monitoring,
            eventBus,
            config,
            NullLogger<DataPipeline>.Instance,
            new PipelinePerformanceOptions
            {
                PartitionCount = 4,
                BatchSize = 32,
                FlushInterval = TimeSpan.FromMilliseconds(50),
                MaxInMemoryBatches = 8,
                AlertingConcurrency = 4
            });

        var channel = Channel.CreateUnbounded<Tick>();
        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 100; i++)
        {
            await channel.Writer.WriteAsync(new Tick
            {
                Source = "Bybit-WebSocket",
                Symbol = "BTCUSDT",
                Price = 10_000m + i,
                Volume = 1m,
                Timestamp = baseTime.AddMilliseconds(i),
                TradeId = $"btc-{i}"
            });
            await channel.Writer.WriteAsync(new Tick
            {
                Source = "Bybit-WebSocket",
                Symbol = "ETHUSDT",
                Price = 2_000m + i,
                Volume = 1m,
                Timestamp = baseTime.AddMilliseconds(i),
                TradeId = $"eth-{i}"
            });
        }

        channel.Writer.Complete();
        await pipeline.StartAsync(channel.Reader, CancellationToken.None);

        var btcPrices = storage.StoredTicks.Where(t => t.Symbol == "BTCUSDT").Select(t => t.Price).ToArray();
        var ethPrices = storage.StoredTicks.Where(t => t.Symbol == "ETHUSDT").Select(t => t.Price).ToArray();
        Assert.Equal(Enumerable.Range(0, 100).Select(i => 10_000m + i), btcPrices);
        Assert.Equal(Enumerable.Range(0, 100).Select(i => 2_000m + i), ethPrices);
    }

    /// <summary>
    /// Проверяет, что обработка разных символов выполняется параллельно в разных партициях.
    /// </summary>
    [Fact]
    public async Task StartAsync_WithDifferentSymbols_ProcessesInParallel()
    {
        const int partitionCount = 2;
        var symbols = SelectSymbolsForDifferentPartitions(partitionCount);
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = symbols
                }
            }
        };

        var pipeline = new DataPipeline(
            new CapturingAggregationService(),
            new CapturingStorage(),
            new DelayedAlertingService(TimeSpan.FromMilliseconds(20)),
            new CapturingMonitoringService(),
            new CapturingMarketDataEventBus(),
            config,
            NullLogger<DataPipeline>.Instance,
            new PipelinePerformanceOptions
            {
                PartitionCount = partitionCount,
                BatchSize = 64,
                FlushInterval = TimeSpan.FromMilliseconds(50),
                MaxInMemoryBatches = 8,
                AlertingConcurrency = 8
            });

        var channel = Channel.CreateUnbounded<Tick>();
        var baseTime = DateTimeOffset.UtcNow;
        const int perSymbolTicks = 20;

        for (var i = 0; i < perSymbolTicks; i++)
        {
            await channel.Writer.WriteAsync(new Tick
            {
                Source = "Bybit-WebSocket",
                Symbol = symbols[0],
                Price = 100m + i,
                Volume = 1m,
                Timestamp = baseTime.AddMilliseconds(i),
                TradeId = $"s1-{i}"
            });
            await channel.Writer.WriteAsync(new Tick
            {
                Source = "Bybit-WebSocket",
                Symbol = symbols[1],
                Price = 200m + i,
                Volume = 1m,
                Timestamp = baseTime.AddMilliseconds(i),
                TradeId = $"s2-{i}"
            });
        }

        channel.Writer.Complete();

        var stopwatch = Stopwatch.StartNew();
        await pipeline.StartAsync(channel.Reader, CancellationToken.None);
        stopwatch.Stop();

        var serialEstimate = TimeSpan.FromMilliseconds(perSymbolTicks * symbols.Length * 20);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(serialEstimate.TotalMilliseconds * 0.8));
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
                Count = 1,
                AverageVolume = tick.Volume
            };
        }
    }

    private sealed class CapturingStorage : IStorage
    {
        public List<RawTick> StoredRawTicks { get; } = new();
        public List<NormalizedTick> StoredTicks { get; } = new();
        public List<AggregatedCandle> StoredAggregates { get; } = new();
        public List<Alert> StoredAlerts { get; } = new();

        public Task StoreRawTicksAsync(IReadOnlyCollection<RawTick> rawTicks, CancellationToken cancellationToken)
        {
            StoredRawTicks.AddRange(rawTicks);
            return Task.CompletedTask;
        }

        public Task StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken)
        {
            return StoreRawTicksAsync(new[] { rawTick }, cancellationToken);
        }

        public Task StoreTicksAsync(IReadOnlyCollection<NormalizedTick> ticks, CancellationToken cancellationToken)
        {
            StoredTicks.AddRange(ticks);
            return Task.CompletedTask;
        }

        public Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
        {
            return StoreTicksAsync(new[] { tick }, cancellationToken);
        }

        public Task StoreAggregatesAsync(IReadOnlyCollection<AggregatedCandle> candles, CancellationToken cancellationToken)
        {
            StoredAggregates.AddRange(candles);
            return Task.CompletedTask;
        }

        public Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
        {
            return StoreAggregatesAsync(new[] { candle }, cancellationToken);
        }

        public Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken)
        {
            StoredAlerts.Add(alert);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<InstrumentMetadata>> GetInstrumentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)Array.Empty<InstrumentMetadata>());
        }

        public Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<AlertRuleConfig>)Array.Empty<AlertRuleConfig>());
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
            PerformanceReport = new PerformanceReport
            {
                WindowMinutes = 5,
                TotalWindowTickCount = 0,
                TotalWindowAggregateCount = 0,
                TotalWindowAvgDelayMs = 0,
                TotalWindowMaxDelayMs = 0,
                TotalWindowTickRatePerSec = 0,
                TotalWindowAggregateRatePerSec = 0,
                SourcesOk = 0,
                SourcesWarn = 0,
                SourcesCritical = 0
            },
            Warnings = Array.Empty<string>()
        };
    }

    private sealed class CapturingMarketDataEventBus : IMarketDataEventBus
    {
        public void PublishTick(NormalizedTick tick)
        {
        }

        public void PublishAggregate(AggregatedCandle candle)
        {
        }

        public void PublishAlert(Alert alert)
        {
        }

        public void PublishMonitoring(MonitoringSnapshot snapshot)
        {
        }

        public async IAsyncEnumerable<MarketDataEvent> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class NoOpAlertingService : IAlertingService
    {
        public Task<IReadOnlyCollection<Alert>> HandleAsync(NormalizedTick tick, MetricsSnapshot metrics, CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<Alert>)Array.Empty<Alert>());
        }
    }

    private sealed class DelayedAlertingService : IAlertingService
    {
        private readonly TimeSpan _delay;

        public DelayedAlertingService(TimeSpan delay)
        {
            _delay = delay;
        }

        public async Task<IReadOnlyCollection<Alert>> HandleAsync(NormalizedTick tick, MetricsSnapshot metrics, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return Array.Empty<Alert>();
        }
    }

    private static string[] SelectSymbolsForDifferentPartitions(int partitionCount)
    {
        var candidates = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT", "ADAUSDT" };

        for (var i = 0; i < candidates.Length; i++)
        {
            for (var j = i + 1; j < candidates.Length; j++)
            {
                var left = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(candidates[i]) % partitionCount);
                var right = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(candidates[j]) % partitionCount);
                if (left != right)
                {
                    return new[] { candidates[i], candidates[j] };
                }
            }
        }

        throw new InvalidOperationException("Не удалось подобрать символы в разные партиции.");
    }
}

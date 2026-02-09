using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Services;

namespace TestTradeService.Benchmarks;

/// <summary>
/// Бенчмарк пропускной способности конвейера обработки тиков.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 2, iterationCount: 6)]
public class DataPipelineThroughputBenchmark
{
    private Tick[] _ticks = Array.Empty<Tick>();
    private MarketInstrumentsConfig _instrumentsConfig = new();
    private PipelinePerformanceOptions _performanceOptions = new();

    /// <summary>
    /// Количество тиков в одном запуске бенчмарка.
    /// </summary>
    [Params(100_000, 500_000)]
    public int TickCount { get; set; }

    /// <summary>
    /// Подготавливает входные данные и конфигурацию перед запуском бенчмарка.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "XRPUSDT", "ADAUSDT", "BNBUSDT", "DOGEUSDT", "LTCUSDT" };
        var now = DateTimeOffset.UtcNow;

        _ticks = Enumerable
            .Range(0, TickCount)
            .Select(index => new Tick
            {
                Source = "Bybit-WebSocket",
                Symbol = symbols[index % symbols.Length],
                Price = 10_000m + (index % 500),
                Volume = 0.1m + (index % 10) * 0.01m,
                Timestamp = now.AddMilliseconds(index),
                TradeId = $"bench-{index}"
            })
            .ToArray();

        _instrumentsConfig = new MarketInstrumentsConfig
        {
            Profiles =
            [
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = symbols
                }
            ]
        };

        _performanceOptions = new PipelinePerformanceOptions
        {
            PartitionCount = Math.Max(1, Environment.ProcessorCount),
            BatchSize = 1024,
            FlushInterval = TimeSpan.FromMilliseconds(250),
            MaxInMemoryBatches = 32,
            AlertingConcurrency = Math.Max(1, Environment.ProcessorCount)
        };
    }

    /// <summary>
    /// Обрабатывает заранее подготовленный поток тиков.
    /// </summary>
    /// <returns>Число обработанных тиков за запуск.</returns>
    [Benchmark]
    public async Task<long> ProcessTicksAsync()
    {
        var pipeline = new DataPipeline(
            new NoOpAggregationService(),
            new NoOpStorage(),
            new NoOpAlertingService(),
            new NoOpMonitoringService(),
            new NoOpMarketDataEventBus(),
            _instrumentsConfig,
            NullLogger<DataPipeline>.Instance,
            _performanceOptions);

        var channel = Channel.CreateUnbounded<Tick>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        foreach (var tick in _ticks)
        {
            await channel.Writer.WriteAsync(tick);
        }

        channel.Writer.Complete();
        await pipeline.StartAsync(channel.Reader, CancellationToken.None);
        return pipeline.ConsumedTickCount;
    }

    private sealed class NoOpAggregationService : IAggregationService
    {
        IEnumerable<AggregatedCandle> IAggregationService.Update(NormalizedTick tick) => [];

        MetricsSnapshot IAggregationService.UpdateMetrics(NormalizedTick tick)
        {
            return new MetricsSnapshot
            {
                Symbol = tick.Symbol,
                WindowStart = tick.Timestamp,
                Window = TimeSpan.FromMinutes(1),
                AveragePrice = tick.Price,
                Volatility = 0m,
                Count = 1,
                AverageVolume = tick.Volume
            };
        }
    }

    private sealed class NoOpStorage : IStorage
    {
        Task IStorage.StoreRawTicksAsync(IReadOnlyCollection<RawTick> rawTicks, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreTicksAsync(IReadOnlyCollection<NormalizedTick> ticks, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreAggregatesAsync(IReadOnlyCollection<AggregatedCandle> candles, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreAlertAsync(Alert alert, CancellationToken cancellationToken) => Task.CompletedTask;

        Task<IReadOnlyCollection<InstrumentMetadata>> IStorage.GetInstrumentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)Array.Empty<InstrumentMetadata>());
        }

        Task<IReadOnlyCollection<AlertRuleConfig>> IStorage.GetAlertRulesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<AlertRuleConfig>)Array.Empty<AlertRuleConfig>());
        }
    }

    private sealed class NoOpAlertingService : IAlertingService
    {
        Task<IReadOnlyCollection<Alert>> IAlertingService.HandleAsync(NormalizedTick tick, MetricsSnapshot metrics, CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<Alert>)Array.Empty<Alert>());
        }
    }

    private sealed class NoOpMonitoringService : IMonitoringService
    {
        private readonly ConcurrentDictionary<string, long> _tickCountBySource = new(StringComparer.OrdinalIgnoreCase);
        private long _totalTicks;

        void IMonitoringService.RecordTick(string sourceName, NormalizedTick tick)
        {
            _tickCountBySource.AddOrUpdate(sourceName, 1, static (_, count) => count + 1);
            Interlocked.Increment(ref _totalTicks);
        }

        void IMonitoringService.RecordAggregate(string sourceName, AggregatedCandle candle)
        {
        }

        void IMonitoringService.RecordDelay(string sourceName, TimeSpan delay)
        {
        }

        MonitoringSnapshot IMonitoringService.Snapshot()
        {
            return new MonitoringSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                ExchangeStats = new Dictionary<MarketExchange, ExchangeStats>(),
                SourceStats = new Dictionary<string, SourceStats>(),
                PerformanceReport = new PerformanceReport
                {
                    WindowMinutes = 1,
                    TotalWindowTickCount = Interlocked.Read(ref _totalTicks),
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
    }

    private sealed class NoOpMarketDataEventBus : IMarketDataEventBus
    {
        void IMarketDataEventBus.PublishTick(NormalizedTick tick)
        {
        }

        void IMarketDataEventBus.PublishAggregate(AggregatedCandle candle)
        {
        }

        void IMarketDataEventBus.PublishAlert(Alert alert)
        {
        }

        void IMarketDataEventBus.PublishMonitoring(MonitoringSnapshot snapshot)
        {
        }

        async IAsyncEnumerable<MarketDataEvent> IMarketDataEventBus.ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}

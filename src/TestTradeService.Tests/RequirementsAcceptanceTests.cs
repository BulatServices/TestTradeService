using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Приемочные тесты соответствия ключевым нефункциональным требованиям.
/// </summary>
public sealed class RequirementsAcceptanceTests
{
    /// <summary>
    /// Проверяет, что конвейер обрабатывает не менее 100 тиков/сек при потоке от трех источников.
    /// </summary>
    [Fact]
    public async Task Load_ThreeSources_AchievesAtLeast100TicksPerSec()
    {
        const int ticksPerSource = 500;
        var totalTicks = ticksPerSource * 3;

        var pipeline = CreatePipeline();
        var channel = Channel.CreateBounded<Tick>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var pipelineTask = pipeline.StartAsync(channel.Reader, CancellationToken.None);
        var timestamp = DateTimeOffset.UtcNow;

        var producers = new[]
        {
            ProduceTicksAsync(channel.Writer, "Bybit-WebSocket", "BTCUSDT", ticksPerSource, timestamp),
            ProduceTicksAsync(channel.Writer, "Coinbase-WebSocket", "BTC-USD", ticksPerSource, timestamp),
            ProduceTicksAsync(channel.Writer, "Kraken-WebSocket", "XBT/USD", ticksPerSource, timestamp)
        };

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(producers);
        channel.Writer.Complete();
        await pipelineTask;
        stopwatch.Stop();

        var throughput = totalTicks / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        Assert.True(throughput >= 100, $"Ожидалось >= 100 ticks/sec, фактически {throughput:F2} ticks/sec.");
    }

    /// <summary>
    /// Проверяет, что падение одного источника не блокирует обработку остальных источников.
    /// </summary>
    [Fact]
    public async Task Load_OneSourceFails_OthersContinueProcessing()
    {
        var source1 = new BurstThenWaitSource("Bybit-WebSocket", MarketExchange.Bybit, "BTCUSDT", 250);
        var source2 = new BurstThenWaitSource("Coinbase-WebSocket", MarketExchange.Coinbase, "BTC-USD", 250);
        var failedSource = new ThrowingSource("Kraken-WebSocket", MarketExchange.Kraken);
        var pipeline = new CountingPipeline();

        var worker = CreateWorker(
            pipeline,
            new IMarketDataSource[] { source1, source2, failedSource },
            TimeSpan.FromSeconds(2));

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(source1.WrittenTicks > 0);
        Assert.True(source2.WrittenTicks > 0);
        Assert.True(failedSource.StartAttempts > 0);
        Assert.True(pipeline.ConsumedTickCount >= source1.WrittenTicks + source2.WrittenTicks);
    }

    /// <summary>
    /// Проверяет, что при штатной остановке конвейер успевает дренировать принятые тики.
    /// </summary>
    [Fact]
    public async Task Shutdown_DrainsAcceptedTicks_BeforeExit()
    {
        var source = new BurstThenWaitSource("Bybit-WebSocket", MarketExchange.Bybit, "BTCUSDT", 300);
        var pipeline = new CountingPipeline(perTickDelay: TimeSpan.FromMilliseconds(1));
        var worker = CreateWorker(pipeline, new[] { source }, TimeSpan.FromSeconds(5));

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => source.WrittenTicks >= 300, TimeSpan.FromSeconds(2));

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(300, source.WrittenTicks);
        Assert.Equal(300, pipeline.ConsumedTickCount);
        Assert.Equal(0, worker.DroppedTicksOnShutdown);
    }

    /// <summary>
    /// Проверяет, что при истечении таймаута дренажа воркер фиксирует потерянные тики.
    /// </summary>
    [Fact]
    public async Task Shutdown_Timeout_ReportsDroppedCount()
    {
        var source = new BurstThenWaitSource("Bybit-WebSocket", MarketExchange.Bybit, "BTCUSDT", 400);
        var pipeline = new CountingPipeline(perTickDelay: TimeSpan.FromMilliseconds(15));
        var worker = CreateWorker(pipeline, new[] { source }, TimeSpan.FromMilliseconds(40));

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => source.WrittenTicks >= 400, TimeSpan.FromSeconds(2));

        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.DroppedTicksOnShutdown > 0);
    }

    /// <summary>
    /// Проверяет, что воркер и конвейер зависят от абстракций, а не от конкретных реализаций.
    /// </summary>
    [Fact]
    public void Architecture_WorkerAndPipeline_DependOnAbstractionsOnly()
    {
        var pipelineCtor = Assert.Single(typeof(DataPipeline).GetConstructors());
        var pipelineParams = pipelineCtor.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(IAlertingService), pipelineParams);
        Assert.DoesNotContain(typeof(AlertingService), pipelineParams);

        var workerCtor = Assert.Single(typeof(TradingSystemWorker).GetConstructors());
        var workerParams = workerCtor.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(IChannelFactory), workerParams);
        Assert.Contains(typeof(IDataPipeline), workerParams);
        Assert.DoesNotContain(typeof(ChannelFactory), workerParams);
        Assert.DoesNotContain(typeof(DataPipeline), workerParams);
    }

    private static DataPipeline CreatePipeline()
    {
        return new DataPipeline(
            new NoOpAggregationService(),
            new NoOpStorage(),
            new NoOpAlertingService(),
            new NoOpMonitoringService(),
            new NoOpMarketDataEventBus(),
            new MarketInstrumentsConfig
            {
                Profiles =
                [
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Bybit,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "BTCUSDT" }
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Coinbase,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "BTC-USD" }
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Kraken,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "XBT/USD" }
                    }
                ]
            },
            NullLogger<DataPipeline>.Instance);
    }

    private static TradingSystemWorker CreateWorker(
        IDataPipeline pipeline,
        IReadOnlyCollection<IMarketDataSource> sources,
        TimeSpan drainTimeout)
    {
        return new TradingSystemWorker(
            new ChannelFactory(),
            sources,
            pipeline,
            new NoOpStorage(),
            new NoOpMonitoringService(),
            new NoOpMarketDataEventBus(),
            NullLogger<TradingSystemWorker>.Instance,
            drainTimeout);
    }

    private static async Task ProduceTicksAsync(
        ChannelWriter<Tick> writer,
        string source,
        string symbol,
        int count,
        DateTimeOffset timestamp)
    {
        for (var i = 0; i < count; i++)
        {
            await writer.WriteAsync(new Tick
            {
                Source = source,
                Symbol = symbol,
                Price = 10_000m + i,
                Volume = 1m,
                Timestamp = timestamp.AddMilliseconds(i),
                TradeId = $"{source}-{i}"
            });
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(10);
        }
    }

    private sealed class CountingPipeline : IDataPipeline
    {
        private readonly TimeSpan _perTickDelay;
        private long _consumedTickCount;

        public CountingPipeline(TimeSpan? perTickDelay = null)
        {
            _perTickDelay = perTickDelay ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Возвращает количество считанных тиков.
        /// </summary>
        public long ConsumedTickCount => Interlocked.Read(ref _consumedTickCount);

        /// <summary>
        /// Считывает тики из канала и увеличивает счетчик.
        /// </summary>
        /// <param name="reader">Канал с тиками.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Задача обработки тиков.</returns>
        public async Task StartAsync(ChannelReader<Tick> reader, CancellationToken cancellationToken)
        {
            await foreach (var _ in reader.ReadAllAsync(cancellationToken))
            {
                Interlocked.Increment(ref _consumedTickCount);
                if (_perTickDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_perTickDelay, cancellationToken);
                }
            }
        }
    }

    private sealed class BurstThenWaitSource : IMarketDataSource
    {
        private readonly string _name;
        private readonly MarketExchange _exchange;
        private readonly string _symbol;
        private readonly int _count;
        private int _writtenTicks;

        public BurstThenWaitSource(string name, MarketExchange exchange, string symbol, int count)
        {
            _name = name;
            _exchange = exchange;
            _symbol = symbol;
            _count = count;
        }

        /// <summary>
        /// Возвращает количество успешно записанных тиков.
        /// </summary>
        public int WrittenTicks => Volatile.Read(ref _writtenTicks);

        async Task IMarketDataSource.StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < _count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteAsync(new Tick
                {
                    Source = _name,
                    Symbol = _symbol,
                    Price = 100m + i,
                    Volume = 1m,
                    Timestamp = now.AddMilliseconds(i),
                    TradeId = $"{_name}-{i}"
                }, cancellationToken);
                Interlocked.Increment(ref _writtenTicks);
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        Task IMarketDataSource.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        string IMarketDataSource.Name => _name;

        MarketExchange IMarketDataSource.Exchange => _exchange;

        MarketDataSourceTransport IMarketDataSource.Transport => MarketDataSourceTransport.WebSocket;
    }

    private sealed class ThrowingSource : IMarketDataSource
    {
        private readonly string _name;
        private readonly MarketExchange _exchange;
        private int _startAttempts;

        public ThrowingSource(string name, MarketExchange exchange)
        {
            _name = name;
            _exchange = exchange;
        }

        /// <summary>
        /// Возвращает количество запусков источника.
        /// </summary>
        public int StartAttempts => Volatile.Read(ref _startAttempts);

        Task IMarketDataSource.StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _startAttempts);
            throw new InvalidOperationException("simulated source failure");
        }

        Task IMarketDataSource.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        string IMarketDataSource.Name => _name;

        MarketExchange IMarketDataSource.Exchange => _exchange;

        MarketDataSourceTransport IMarketDataSource.Transport => MarketDataSourceTransport.WebSocket;
    }

    private sealed class NoOpAlertingService : IAlertingService
    {
        Task<IReadOnlyCollection<Alert>> IAlertingService.HandleAsync(NormalizedTick tick, MetricsSnapshot metrics, CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<Alert>)Array.Empty<Alert>());
        }
    }

    private sealed class NoOpAggregationService : IAggregationService
    {
        IEnumerable<AggregatedCandle> IAggregationService.Update(NormalizedTick tick) => Array.Empty<AggregatedCandle>();

        MetricsSnapshot IAggregationService.UpdateMetrics(NormalizedTick tick)
        {
            return new MetricsSnapshot
            {
                Symbol = tick.Symbol,
                WindowStart = tick.Timestamp,
                Window = TimeSpan.FromMinutes(1),
                AveragePrice = tick.Price,
                Volatility = 0,
                Count = 1,
                AverageVolume = tick.Volume
            };
        }
    }

    private sealed class NoOpStorage : IStorage
    {
        Task IStorage.StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        Task IStorage.StoreAlertAsync(Alert alert, CancellationToken cancellationToken) => Task.CompletedTask;

        Task<IReadOnlyCollection<InstrumentMetadata>> IStorage.GetInstrumentsAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)Array.Empty<InstrumentMetadata>());

        Task<IReadOnlyCollection<AlertRuleConfig>> IStorage.GetAlertRulesAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyCollection<AlertRuleConfig>)Array.Empty<AlertRuleConfig>());
    }

    private sealed class NoOpMonitoringService : IMonitoringService
    {
        void IMonitoringService.RecordTick(string sourceName, NormalizedTick tick)
        {
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

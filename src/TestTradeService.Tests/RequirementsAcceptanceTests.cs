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
/// РџСЂРёРµРјРѕС‡РЅС‹Рµ С‚РµСЃС‚С‹ СЃРѕРѕС‚РІРµС‚СЃС‚РІРёСЏ РєР»СЋС‡РµРІС‹Рј РЅРµС„СѓРЅРєС†РёРѕРЅР°Р»СЊРЅС‹Рј С‚СЂРµР±РѕРІР°РЅРёСЏРј.
/// </summary>
public sealed class RequirementsAcceptanceTests
{
    private static readonly string[] BybitSymbols =
    [
        "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT", "ADAUSDT", "DOGEUSDT", "LTCUSDT",
        "DOTUSDT", "AVAXUSDT", "LINKUSDT", "MATICUSDT"
    ];
    private static readonly string[] CoinbaseSymbols =
    [
        "BTC-USD", "ETH-USD", "SOL-USD", "AVAX-USD", "XRP-USD", "ADA-USD", "DOGE-USD", "LTC-USD",
        "DOT-USD", "LINK-USD", "BCH-USD", "ATOM-USD"
    ];
    private static readonly string[] KrakenSymbols =
    [
        "XBT/USD", "ETH/USD", "SOL/USD", "ADA/USD", "XRP/USD", "DOGE/USD", "LTC/USD", "DOT/USD",
        "LINK/USD", "AVAX/USD", "BCH/USD", "ATOM/USD"
    ];

    /// <summary>
    /// РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ РєРѕРЅРІРµР№РµСЂ РѕР±СЂР°Р±Р°С‚С‹РІР°РµС‚ РЅРµ РјРµРЅРµРµ 100 С‚РёРєРѕРІ/СЃРµРє РїСЂРё РїРѕС‚РѕРєРµ РѕС‚ С‚СЂРµС… РёСЃС‚РѕС‡РЅРёРєРѕРІ.
    /// </summary>
    [Fact]
    public async Task Load_ThreeSources_AchievesAtLeast100TicksPerSec()
    {
        const int ticksPerSymbol = 250;
        var totalTicks = (BybitSymbols.Length + CoinbaseSymbols.Length + KrakenSymbols.Length) * ticksPerSymbol;

        var pipeline = CreatePipeline();
        var channel = Channel.CreateBounded<Tick>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var pipelineTask = pipeline.StartAsync(channel.Reader, CancellationToken.None);
        var timestamp = DateTimeOffset.UtcNow;

        var producers = BybitSymbols
            .Select(symbol => ProduceTicksAsync(channel.Writer, "Bybit-WebSocket", symbol, ticksPerSymbol, timestamp))
            .Concat(CoinbaseSymbols.Select(symbol =>
                ProduceTicksAsync(channel.Writer, "Coinbase-WebSocket", symbol, ticksPerSymbol, timestamp)))
            .Concat(KrakenSymbols.Select(symbol =>
                ProduceTicksAsync(channel.Writer, "Kraken-WebSocket", symbol, ticksPerSymbol, timestamp)))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(producers);
        channel.Writer.Complete();
        await pipelineTask;
        stopwatch.Stop();

        var throughput = totalTicks / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        Assert.True(throughput >= 100, $"РћР¶РёРґР°Р»РѕСЃСЊ >= 100 ticks/sec, С„Р°РєС‚РёС‡РµСЃРєРё {throughput:F2} ticks/sec.");
    }

    /// <summary>
    /// Проверяет, что оптимизированные настройки конвейера дают не менее чем трехкратный прирост throughput относительно baseline.
    /// </summary>
    [Fact]
    public async Task Load_OptimizedPipeline_AchievesAtLeastThreeTimesBaselineThroughput()
    {
        const int ticksPerSymbol = 120;
        var timestamp = DateTimeOffset.UtcNow;

        var baselineThroughput = await MeasureThroughputAsync(
            CreatePipeline(
                new SimulatedBatchLatencyStorage(TimeSpan.FromMilliseconds(2)),
                new PipelinePerformanceOptions
                {
                    PartitionCount = 1,
                    BatchSize = 1,
                    FlushInterval = TimeSpan.FromMilliseconds(10),
                    MaxInMemoryBatches = 4,
                    AlertingConcurrency = 1
                }),
            BybitSymbols,
            ticksPerSymbol,
            timestamp);

        var optimizedThroughput = await MeasureThroughputAsync(
            CreatePipeline(
                new SimulatedBatchLatencyStorage(TimeSpan.FromMilliseconds(2)),
                new PipelinePerformanceOptions
                {
                    PartitionCount = Math.Max(2, Environment.ProcessorCount),
                    BatchSize = 256,
                    FlushInterval = TimeSpan.FromMilliseconds(250),
                    MaxInMemoryBatches = 16,
                    AlertingConcurrency = Math.Max(2, Environment.ProcessorCount / 2)
                }),
            BybitSymbols,
            ticksPerSymbol,
            timestamp);

        var expected = Math.Max(100d, baselineThroughput * 3d);
        Assert.True(
            optimizedThroughput >= expected,
            $"Ожидалось >= {expected:F2} ticks/sec, baseline={baselineThroughput:F2}, optimized={optimizedThroughput:F2}.");
    }
    /// <summary>
    /// РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ РїР°РґРµРЅРёРµ РѕРґРЅРѕРіРѕ РёСЃС‚РѕС‡РЅРёРєР° РЅРµ Р±Р»РѕРєРёСЂСѓРµС‚ РѕР±СЂР°Р±РѕС‚РєСѓ РѕСЃС‚Р°Р»СЊРЅС‹С… РёСЃС‚РѕС‡РЅРёРєРѕРІ.
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
    /// РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ РїСЂРё С€С‚Р°С‚РЅРѕР№ РѕСЃС‚Р°РЅРѕРІРєРµ РєРѕРЅРІРµР№РµСЂ СѓСЃРїРµРІР°РµС‚ РґСЂРµРЅРёСЂРѕРІР°С‚СЊ РїСЂРёРЅСЏС‚С‹Рµ С‚РёРєРё.
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
    /// РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ РїСЂРё РёСЃС‚РµС‡РµРЅРёРё С‚Р°Р№РјР°СѓС‚Р° РґСЂРµРЅР°Р¶Р° РІРѕСЂРєРµСЂ С„РёРєСЃРёСЂСѓРµС‚ РїРѕС‚РµСЂСЏРЅРЅС‹Рµ С‚РёРєРё.
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
    /// РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ РІРѕСЂРєРµСЂ Рё РєРѕРЅРІРµР№РµСЂ Р·Р°РІРёСЃСЏС‚ РѕС‚ Р°Р±СЃС‚СЂР°РєС†РёР№, Р° РЅРµ РѕС‚ РєРѕРЅРєСЂРµС‚РЅС‹С… СЂРµР°Р»РёР·Р°С†РёР№.
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

    private static DataPipeline CreatePipeline(IStorage? storage = null, PipelinePerformanceOptions? performanceOptions = null)
    {
        return new DataPipeline(
            new NoOpAggregationService(),
            storage ?? new NoOpStorage(),
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
                        Symbols = BybitSymbols
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Coinbase,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = CoinbaseSymbols
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Kraken,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = KrakenSymbols
                    }
                ]
            },
            NullLogger<DataPipeline>.Instance,
            performanceOptions);
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

    private static async Task<double> MeasureThroughputAsync(
        DataPipeline pipeline,
        IReadOnlyCollection<string> symbols,
        int ticksPerSymbol,
        DateTimeOffset timestamp)
    {
        var channel = Channel.CreateBounded<Tick>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var pipelineTask = pipeline.StartAsync(channel.Reader, CancellationToken.None);
        var producers = symbols
            .Select(symbol => ProduceTicksAsync(channel.Writer, "Bybit-WebSocket", symbol, ticksPerSymbol, timestamp))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(producers);
        channel.Writer.Complete();
        await pipelineTask;
        stopwatch.Stop();

        var totalTicks = symbols.Count * ticksPerSymbol;
        return totalTicks / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
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

        /// <summary>
        /// РЎРѕР·РґР°РµС‚ С‚РµСЃС‚РѕРІС‹Р№ РєРѕРЅРІРµР№РµСЂ СЃ РѕРїС†РёРѕРЅР°Р»СЊРЅРѕР№ Р·Р°РґРµСЂР¶РєРѕР№ РѕР±СЂР°Р±РѕС‚РєРё РѕРґРЅРѕРіРѕ С‚РёРєР°.
        /// </summary>
        /// <param name="perTickDelay">Р—Р°РґРµСЂР¶РєР° РЅР° РѕР±СЂР°Р±РѕС‚РєСѓ РѕРґРЅРѕРіРѕ С‚РёРєР°; РµСЃР»Рё <see langword="null"/>, Р·Р°РґРµСЂР¶РєР° РЅРµ РїСЂРёРјРµРЅСЏРµС‚СЃСЏ.</param>
        public CountingPipeline(TimeSpan? perTickDelay = null)
        {
            _perTickDelay = perTickDelay ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Р’РѕР·РІСЂР°С‰Р°РµС‚ РєРѕР»РёС‡РµСЃС‚РІРѕ СЃС‡РёС‚Р°РЅРЅС‹С… С‚РёРєРѕРІ.
        /// </summary>
        public long ConsumedTickCount => Interlocked.Read(ref _consumedTickCount);

        /// <summary>
        /// РЎС‡РёС‚С‹РІР°РµС‚ С‚РёРєРё РёР· РєР°РЅР°Р»Р° Рё СѓРІРµР»РёС‡РёРІР°РµС‚ СЃС‡РµС‚С‡РёРє.
        /// </summary>
        /// <param name="reader">РљР°РЅР°Р» СЃ С‚РёРєР°РјРё.</param>
        /// <param name="cancellationToken">РўРѕРєРµРЅ РѕС‚РјРµРЅС‹.</param>
        /// <returns>Р—Р°РґР°С‡Р° РѕР±СЂР°Р±РѕС‚РєРё С‚РёРєРѕРІ.</returns>
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

        /// <summary>
        /// РЎРѕР·РґР°РµС‚ С‚РµСЃС‚РѕРІС‹Р№ РёСЃС‚РѕС‡РЅРёРє, РєРѕС‚РѕСЂС‹Р№ РїСѓР±Р»РёРєСѓРµС‚ Р·Р°РґР°РЅРЅРѕРµ РєРѕР»РёС‡РµСЃС‚РІРѕ С‚РёРєРѕРІ Рё РѕР¶РёРґР°РµС‚ РѕСЃС‚Р°РЅРѕРІРєРё.
        /// </summary>
        /// <param name="name">РРјСЏ РёСЃС‚РѕС‡РЅРёРєР°.</param>
        /// <param name="exchange">Р‘РёСЂР¶Р° РёСЃС‚РѕС‡РЅРёРєР°.</param>
        /// <param name="symbol">РўРёРєРµСЂ РґР»СЏ РіРµРЅРµСЂР°С†РёРё С‚РёРєРѕРІ.</param>
        /// <param name="count">РљРѕР»РёС‡РµСЃС‚РІРѕ С‚РёРєРѕРІ РґР»СЏ РїСѓР±Р»РёРєР°С†РёРё.</param>
        public BurstThenWaitSource(string name, MarketExchange exchange, string symbol, int count)
        {
            _name = name;
            _exchange = exchange;
            _symbol = symbol;
            _count = count;
        }

        /// <summary>
        /// Р’РѕР·РІСЂР°С‰Р°РµС‚ РєРѕР»РёС‡РµСЃС‚РІРѕ СѓСЃРїРµС€РЅРѕ Р·Р°РїРёСЃР°РЅРЅС‹С… С‚РёРєРѕРІ.
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

        /// <summary>
        /// РЎРѕР·РґР°РµС‚ С‚РµСЃС‚РѕРІС‹Р№ РёСЃС‚РѕС‡РЅРёРє, РєРѕС‚РѕСЂС‹Р№ РІС‹Р±СЂР°СЃС‹РІР°РµС‚ РёСЃРєР»СЋС‡РµРЅРёРµ РїСЂРё Р·Р°РїСѓСЃРєРµ.
        /// </summary>
        /// <param name="name">РРјСЏ РёСЃС‚РѕС‡РЅРёРєР°.</param>
        /// <param name="exchange">Р‘РёСЂР¶Р° РёСЃС‚РѕС‡РЅРёРєР°.</param>
        public ThrowingSource(string name, MarketExchange exchange)
        {
            _name = name;
            _exchange = exchange;
        }

        /// <summary>
        /// Р’РѕР·РІСЂР°С‰Р°РµС‚ РєРѕР»РёС‡РµСЃС‚РІРѕ Р·Р°РїСѓСЃРєРѕРІ РёСЃС‚РѕС‡РЅРёРєР°.
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
            => Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)Array.Empty<InstrumentMetadata>());

        Task<IReadOnlyCollection<AlertRuleConfig>> IStorage.GetAlertRulesAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyCollection<AlertRuleConfig>)Array.Empty<AlertRuleConfig>());
    }

    private sealed class SimulatedBatchLatencyStorage : IStorage
    {
        private readonly TimeSpan _batchDelay;

        public SimulatedBatchLatencyStorage(TimeSpan batchDelay)
        {
            _batchDelay = batchDelay;
        }

        public async Task StoreRawTicksAsync(IReadOnlyCollection<RawTick> rawTicks, CancellationToken cancellationToken)
        {
            if (rawTicks.Count > 0)
            {
                await Task.Delay(_batchDelay, cancellationToken);
            }
        }

        public Task StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken)
            => StoreRawTicksAsync(new[] { rawTick }, cancellationToken);

        public async Task StoreTicksAsync(IReadOnlyCollection<NormalizedTick> ticks, CancellationToken cancellationToken)
        {
            if (ticks.Count > 0)
            {
                await Task.Delay(_batchDelay, cancellationToken);
            }
        }

        public Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
            => StoreTicksAsync(new[] { tick }, cancellationToken);

        public async Task StoreAggregatesAsync(IReadOnlyCollection<AggregatedCandle> candles, CancellationToken cancellationToken)
        {
            if (candles.Count > 0)
            {
                await Task.Delay(_batchDelay, cancellationToken);
            }
        }

        public Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
            => StoreAggregatesAsync(new[] { candle }, cancellationToken);

        public Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyCollection<InstrumentMetadata>> GetInstrumentsAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)Array.Empty<InstrumentMetadata>());

        public Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken)
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



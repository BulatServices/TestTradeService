using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Management;
using TestTradeService.Models;
using TestTradeService.Monitoring;
using TestTradeService.Monitoring.Configuration;
using TestTradeService.Services;
using TestTradeService.Storage;

var demoMode = string.Equals(Environment.GetEnvironmentVariable("DEMO_MODE"), "true", StringComparison.OrdinalIgnoreCase);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<ChannelFactory>();
        services.AddSingleton(demoMode
            ? new MarketInstrumentsConfig
            {
                Profiles = new[]
                {
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Demo,
                        MarketType = MarketType.Spot,
                        Symbols = new[] { "BTC-USD", "ETH-USD", "SOL-USD" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Demo,
                        MarketType = MarketType.Perp,
                        Symbols = new[] { "BTC-USD", "ETH-USD", "XRP-USD" },
                        TargetUpdateInterval = TimeSpan.FromMilliseconds(100)
                    }
                }
            }
            : new MarketInstrumentsConfig
            {
                Profiles = new[]
                {
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Kraken,
                        MarketType = MarketType.Spot,
                        Symbols = new[] { "XBT/USD", "ETH/USD" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Coinbase,
                        MarketType = MarketType.Spot,
                        Symbols = new[] { "BTC-USD", "ETH-USD", "SOL-USD" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Bybit,
                        MarketType = MarketType.Spot,
                        Symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    }
                }
            });
        services.AddSingleton(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromSeconds(2)
        });
        services.AddSingleton<IStorage, InMemoryStorage>();
        services.AddSingleton<IMonitoringService, MonitoringService>();
        services.AddSingleton<IAggregationService, AggregationService>();
        services.AddSingleton<IAlertRule, PriceThresholdRule>();
        services.AddSingleton<IAlertRule, VolumeSpikeRule>();
        services.AddSingleton<INotifier, ConsoleNotifier>();
        services.AddSingleton<INotifier, FileNotifier>();
        services.AddSingleton<INotifier, EmailStubNotifier>();
        services.AddSingleton<AlertingService>();
        services.AddSingleton<DataPipeline>();
        services.AddSingleton<TradeCursorStore>();

        RegisterMarketDataSources(services, demoMode);
        services.AddIngestionSubsystem();
        services.AddHostedService<TradingSystemWorker>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();

static void RegisterMarketDataSources(IServiceCollection services, bool demoMode)
{
    var sourcesAssembly = typeof(IMarketDataSource).Assembly;

    static bool ImplementsMarketDataSource(Type type) =>
        type is { IsAbstract: false, IsInterface: false }
        && typeof(IMarketDataSource).IsAssignableFrom(type);

    static bool IsDemoSourceNamespace(string? ns) =>
        string.Equals(ns, "TestTradeService.Services", StringComparison.Ordinal);

    static bool IsExchangeSourceNamespace(string? ns) =>
        ns?.StartsWith("TestTradeService.Services.Exchanges.", StringComparison.Ordinal) == true;

    var sourceTypes = sourcesAssembly
        .GetTypes()
        .Where(ImplementsMarketDataSource)
        .Where(t => demoMode ? IsDemoSourceNamespace(t.Namespace) : IsExchangeSourceNamespace(t.Namespace))
        .OrderBy(t => t.FullName, StringComparer.Ordinal);

    foreach (var sourceType in sourceTypes)
        services.AddSingleton(typeof(IMarketDataSource), sourceType);
}

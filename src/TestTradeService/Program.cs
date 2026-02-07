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
using TestTradeService.Services.Exchanges.Bybit;
using TestTradeService.Services.Exchanges.Coinbase;
using TestTradeService.Services.Exchanges.Kraken;
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

        if (demoMode)
        {
            services.AddSingleton<IMarketDataSource, DemoRestPollingSource>();
            services.AddSingleton<IMarketDataSource, DemoWebSocketSource>();
        }
        else
        {
            services.AddSingleton<IMarketDataSource, KrakenTradesWebSocketSource>();
            services.AddSingleton<IMarketDataSource, KrakenTradesRestPollingSource>();
            services.AddSingleton<IMarketDataSource, CoinbaseMatchesWebSocketSource>();
            services.AddSingleton<IMarketDataSource, CoinbaseTradesRestPollingSource>();
            services.AddSingleton<IMarketDataSource, BybitPublicTradesWebSocketSource>();
            services.AddSingleton<IMarketDataSource, BybitTradesRestPollingSource>();
        }
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Management;
using TestTradeService.Monitoring;
using TestTradeService.Services;
using TestTradeService.Storage;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<ChannelFactory>();
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
        services.AddSingleton<IMarketDataSource, RestPollingSource>();
        services.AddSingleton<IMarketDataSource, WebSocketSource>();
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

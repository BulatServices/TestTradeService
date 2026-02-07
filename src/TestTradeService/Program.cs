using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Management;
using TestTradeService.Models;
using TestTradeService.Monitoring;
using TestTradeService.Monitoring.Configuration;
using TestTradeService.Services;
using TestTradeService.Storage;
using TestTradeService.Storage.Configuration;

var demoMode = string.Equals(Environment.GetEnvironmentVariable("DEMO_MODE"), "true", StringComparison.OrdinalIgnoreCase);

var bootstrapConfiguration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var databaseOptions = bootstrapConfiguration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var databaseEnabled = databaseOptions.HasBothConnections();

var instrumentsConfig = DefaultConfigurationFactory.CreateInstruments(demoMode);
var alertRuleConfigs = DefaultConfigurationFactory.CreateAlertRules();
MetadataDataSource? metadataDataSource = null;
TimeseriesDataSource? timeseriesDataSource = null;

if (databaseEnabled)
{
    metadataDataSource = new MetadataDataSource(NpgsqlDataSource.Create(databaseOptions.MetadataConnectionString));
    timeseriesDataSource = new TimeseriesDataSource(NpgsqlDataSource.Create(databaseOptions.TimeseriesConnectionString));

    if (databaseOptions.AutoMigrate)
    {
        var runner = new SqlMigrationRunner(metadataDataSource, timeseriesDataSource);
        await runner.RunAsync(CancellationToken.None);
    }

    var repository = new PostgresConfigurationRepository(metadataDataSource);
    var loadedInstruments = await repository.GetMarketInstrumentsConfigAsync(demoMode, CancellationToken.None);
    var loadedRules = await repository.GetAlertRulesAsync(CancellationToken.None);

    if (loadedInstruments.Profiles.Count > 0)
    {
        instrumentsConfig = loadedInstruments;
    }

    if (loadedRules.Count > 0)
    {
        alertRuleConfigs = loadedRules;
    }
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<ChannelFactory>();
        services.AddSingleton(instrumentsConfig);
        services.AddSingleton(Options.Create(databaseOptions));

        services.AddSingleton(new MonitoringSlaConfig
        {
            MaxTickDelay = TimeSpan.FromSeconds(2)
        });

        if (databaseEnabled && metadataDataSource is not null && timeseriesDataSource is not null)
        {
            services.AddSingleton(metadataDataSource);
            services.AddSingleton(timeseriesDataSource);
            services.AddSingleton<SqlMigrationRunner>();
            services.AddSingleton<IConfigurationRepository, PostgresConfigurationRepository>();
            services.AddSingleton<IStorage, HybridStorage>();
            services.AddHostedService<MigrationHostedService>();
        }
        else
        {
            services.AddSingleton<IStorage, InMemoryStorage>();
        }

        services.AddSingleton<IAlertRuleConfigProvider>(_ => new AlertRuleConfigProvider(alertRuleConfigs));
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
    {
        services.AddSingleton(typeof(IMarketDataSource), sourceType);
    }
}

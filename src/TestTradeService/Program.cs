using Microsoft.Extensions.Options;
using Npgsql;
using TestTradeService.Api;
using TestTradeService.Ingestion.Management;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Monitoring;
using TestTradeService.Monitoring.Configuration;
using TestTradeService.Realtime;
using TestTradeService.Services;
using TestTradeService.Storage;
using TestTradeService.Storage.Configuration;
using TestTradeService.Storage.Repositories;
using TestTradeService.Storage.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var metadataEnabled = databaseOptions.HasMetadataConnection();
var fullDatabaseEnabled = databaseOptions.HasBothConnections();

var instrumentsConfig = DefaultConfigurationFactory.CreateInstruments();
var alertRuleConfigs = DefaultConfigurationFactory.CreateAlertRules();
MetadataDataSource? metadataDataSource = null;
TimeseriesDataSource? timeseriesDataSource = null;

if (metadataEnabled)
{
    metadataDataSource = new MetadataDataSource(NpgsqlDataSource.Create(databaseOptions.MetadataConnectionString));
}

if (fullDatabaseEnabled)
{
    timeseriesDataSource = new TimeseriesDataSource(NpgsqlDataSource.Create(databaseOptions.TimeseriesConnectionString));

    if (databaseOptions.AutoMigrate)
    {
        var runner = new SqlMigrationRunner(metadataDataSource!, timeseriesDataSource);
        await runner.RunAsync(CancellationToken.None);
    }
}

if (metadataEnabled)
{
    try
    {
        var repository = new PostgresConfigurationRepository(metadataDataSource!);
        var loadedInstruments = await repository.GetMarketInstrumentsConfigAsync(CancellationToken.None);
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
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load configuration from metadata database: {ex.Message}");
    }
}

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<IChannelFactory, ChannelFactory>();
builder.Services.AddSingleton(instrumentsConfig);
builder.Services.AddSingleton(Options.Create(databaseOptions));

builder.Services.AddSingleton(new MonitoringSlaConfig
{
    MaxTickDelay = TimeSpan.FromSeconds(2)
});
var pipelinePerformanceOptions = builder.Configuration.GetSection("PipelinePerformance").Get<PipelinePerformanceOptions>() ?? new PipelinePerformanceOptions();
builder.Services.AddSingleton(pipelinePerformanceOptions);

if (metadataEnabled && metadataDataSource is not null)
{
    builder.Services.AddSingleton(metadataDataSource!);
}

if (fullDatabaseEnabled && metadataDataSource is not null && timeseriesDataSource is not null)
{
    builder.Services.AddSingleton(timeseriesDataSource);
    builder.Services.AddSingleton<SqlMigrationRunner>();
    builder.Services.AddSingleton<IConfigurationRepository, PostgresConfigurationRepository>();
    builder.Services.AddSingleton<IStorage, HybridStorage>();
    builder.Services.AddHostedService<MigrationHostedService>();

    builder.Services.AddSingleton<IProcessedReadRepository, ProcessedReadRepository>();
}
else
{
    builder.Services.AddSingleton<IStorage, InMemoryStorage>();
    builder.Services.AddSingleton<IProcessedReadRepository, InMemoryProcessedReadRepository>();
}

if (metadataEnabled && metadataDataSource is not null)
{
    builder.Services.AddSingleton<IAlertReadRepository, AlertReadRepository>();
    builder.Services.AddSingleton<IAlertRuleWriteRepository, AlertRuleWriteRepository>();
}
else
{
    builder.Services.AddSingleton<InMemoryAlertRepository>();
    builder.Services.AddSingleton<IAlertReadRepository>(sp => sp.GetRequiredService<InMemoryAlertRepository>());
    builder.Services.AddSingleton<IAlertRuleWriteRepository>(sp => sp.GetRequiredService<InMemoryAlertRepository>());
}

builder.Services.AddSingleton<IMutableAlertRuleConfigProvider>(_ => new AlertRuleConfigProvider(alertRuleConfigs));
builder.Services.AddSingleton<IAlertRuleConfigProvider>(sp => sp.GetRequiredService<IMutableAlertRuleConfigProvider>());
builder.Services.AddSingleton<IMonitoringService, MonitoringService>();
builder.Services.AddSingleton<IAggregationService, AggregationService>();
builder.Services.AddSingleton<IAlertRule, PriceThresholdRule>();
builder.Services.AddSingleton<IAlertRule, VolumeSpikeRule>();
builder.Services.AddSingleton<INotifier, ConsoleNotifier>();
builder.Services.AddSingleton<INotifier, FileNotifier>();
builder.Services.AddSingleton<INotifier, EmailStubNotifier>();
builder.Services.AddSingleton<IAlertingService, AlertingService>();
builder.Services.AddSingleton<IMarketDataEventBus, MarketDataEventBus>();
builder.Services.AddSingleton<IDataPipeline, DataPipeline>();
builder.Services.AddSingleton<TradeCursorStore>();
builder.Services.AddSingleton<ISourceConfigService, SourceConfigService>();

builder.Services.AddSingleton<MarketHubConnectionStateStore>();
builder.Services.AddHostedService<MarketDataBroadcaster>();

RegisterMarketDataSources(builder.Services);
builder.Services.AddIngestionSubsystem();

builder.Services.AddSingleton<TradingSystemWorker>();
builder.Services.AddSingleton<IRuntimeReconfigurationService>(sp => sp.GetRequiredService<TradingSystemWorker>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TradingSystemWorker>());

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseRouting();
app.UseCors("ApiCors");

app.MapControllers();
app.MapHub<MarketDataHub>("/hubs/market-data");
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

await app.RunAsync();

static void RegisterMarketDataSources(IServiceCollection services)
{
    var sourcesAssembly = typeof(IMarketDataSource).Assembly;

    static bool ImplementsMarketDataSource(Type type) =>
        type is { IsAbstract: false, IsInterface: false }
        && typeof(IMarketDataSource).IsAssignableFrom(type);

    static bool IsExchangeSourceNamespace(string? ns) =>
        ns?.StartsWith("TestTradeService.Services.Exchanges.", StringComparison.Ordinal) == true;

    var sourceTypes = sourcesAssembly
        .GetTypes()
        .Where(ImplementsMarketDataSource)
        .Where(t => IsExchangeSourceNamespace(t.Namespace))
        .OrderBy(t => t.FullName, StringComparer.Ordinal);

    foreach (var sourceType in sourceTypes)
    {
        services.AddSingleton(typeof(IMarketDataSource), sourceType);
    }
}

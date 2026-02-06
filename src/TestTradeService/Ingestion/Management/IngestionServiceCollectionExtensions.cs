using Microsoft.Extensions.DependencyInjection;
using TestTradeService.Ingestion.Factory;
using TestTradeService.Ingestion.Monitoring;

namespace TestTradeService.Ingestion.Management;

/// <summary>
/// Расширения DI для регистрации подсистемы ingestion.
/// </summary>
public static class IngestionServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует фабрику, менеджер каналов, трекер здоровья и HTTP-клиент.
    /// </summary>
    public static IServiceCollection AddIngestionSubsystem(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IChannelFactory, ChannelFactory>();
        services.AddSingleton<SourceHealthTracker>();
        services.AddSingleton<ChannelManager>();
        return services;
    }
}

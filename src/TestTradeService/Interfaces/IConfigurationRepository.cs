using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Репозиторий чтения конфигурации торговой системы из хранилища метаданных.
/// </summary>
public interface IConfigurationRepository
{
    /// <summary>
    /// Загружает конфигурацию инструментов по биржам и типам рынков.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Конфигурация инструментов для подсистем ingestion и фильтрации.</returns>
    Task<MarketInstrumentsConfig> GetMarketInstrumentsConfigAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Загружает конфигурацию правил алертинга с параметрами.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор конфигураций правил.</returns>
    Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken);
}

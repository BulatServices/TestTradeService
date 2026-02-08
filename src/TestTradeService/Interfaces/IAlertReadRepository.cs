using TestTradeService.Api.Contracts;

namespace TestTradeService.Interfaces;

/// <summary>
/// Репозиторий чтения алертов и правил алертинга.
/// </summary>
public interface IAlertReadRepository
{
    /// <summary>
    /// Загружает алерты по фильтрам.
    /// </summary>
    /// <param name="request">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с алертами.</returns>
    Task<AlertsResponseDto> GetAlertsAsync(AlertsQuery request, CancellationToken cancellationToken);

    /// <summary>
    /// Загружает правила алертинга.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с правилами.</returns>
    Task<AlertRulesResponseDto> GetAlertRulesAsync(CancellationToken cancellationToken);
}

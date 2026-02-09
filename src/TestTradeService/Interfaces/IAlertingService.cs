using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Контракт сервиса алертинга для обработки правил и отправки уведомлений.
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Обрабатывает тик, формирует алерты и отправляет их в целевые каналы.
    /// </summary>
    /// <param name="tick">Текущий нормализованный тик.</param>
    /// <param name="metrics">Текущие метрики по инструменту.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список сформированных алертов.</returns>
    Task<IReadOnlyCollection<Alert>> HandleAsync(NormalizedTick tick, MetricsSnapshot metrics, CancellationToken cancellationToken);
}

using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Описывает правило генерации алерта.
/// </summary>
public interface IAlertRule
{
    /// <summary>
    /// Имя правила алертинга.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Проверяет, срабатывает ли правило для текущих данных.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="metrics">Рассчитанные метрики по инструменту.</param>
    /// <returns><c>true</c>, если правило сработало.</returns>
    bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics);

    /// <summary>
    /// Создает объект алерта по сработавшему правилу.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="metrics">Рассчитанные метрики.</param>
    /// <returns>Сформированный алерт.</returns>
    Alert CreateAlert(NormalizedTick tick, MetricsSnapshot metrics);
}

using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Сервис расчета агрегатов и производных метрик по тикам.
/// </summary>
public interface IAggregationService
{
    /// <summary>
    /// Обновляет агрегаты по входящему тику.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <returns>Список завершенных свечей, готовых к сохранению.</returns>
    IEnumerable<AggregatedCandle> Update(NormalizedTick tick);

    /// <summary>
    /// Обновляет расчетные метрики по инструменту.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <returns>Снимок метрик после обработки тика.</returns>
    MetricsSnapshot UpdateMetrics(NormalizedTick tick);
}

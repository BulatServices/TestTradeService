using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Сервис сбора эксплуатационных метрик и состояния системы.
/// </summary>
public interface IMonitoringService
{
    /// <summary>
    /// Регистрирует обработанный тик.
    /// </summary>
    void RecordTick(string sourceName, NormalizedTick tick);

    /// <summary>
    /// Регистрирует сформированный агрегат.
    /// </summary>
    void RecordAggregate(string sourceName, AggregatedCandle candle);

    /// <summary>
    /// Регистрирует задержку между временем тика и временем обработки.
    /// </summary>
    void RecordDelay(string sourceName, TimeSpan delay);

    /// <summary>
    /// Возвращает текущий снимок мониторинга.
    /// </summary>
    MonitoringSnapshot Snapshot();
}

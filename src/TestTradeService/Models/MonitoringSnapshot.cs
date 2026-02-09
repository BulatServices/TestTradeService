namespace TestTradeService.Models;

/// <summary>
/// Снимок состояния мониторинга системы.
/// </summary>
public sealed record MonitoringSnapshot
{
    /// <summary>
    /// Время формирования снимка.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Статистика по биржам.
    /// </summary>
    public required IReadOnlyDictionary<MarketExchange, ExchangeStats> ExchangeStats { get; init; }

    /// <summary>
    /// Статистика по источникам.
    /// </summary>
    public required IReadOnlyDictionary<string, SourceStats> SourceStats { get; init; }

    /// <summary>
    /// Сводный отчет о производительности за окно.
    /// </summary>
    public required PerformanceReport PerformanceReport { get; init; }

    /// <summary>
    /// Список предупреждений.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// Статистика производительности в разрезе одной биржи.
/// </summary>
public sealed record ExchangeStats
{
    /// <summary>
    /// Биржа (торговая площадка).
    /// </summary>
    public required MarketExchange Exchange { get; init; }

    /// <summary>
    /// Количество обработанных тиков с момента старта процесса.
    /// </summary>
    public required long TickCount { get; init; }

    /// <summary>
    /// Количество сформированных агрегатов с момента старта процесса.
    /// </summary>
    public required long AggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка обработки в миллисекундах с момента старта процесса.
    /// </summary>
    public required double AverageDelayMs { get; init; }

    /// <summary>
    /// Время последнего полученного тика.
    /// </summary>
    public required DateTimeOffset LastTickTime { get; init; }

    /// <summary>
    /// Количество тиков за rolling-окно.
    /// </summary>
    public required long WindowTickCount { get; init; }

    /// <summary>
    /// Количество агрегатов за rolling-окно.
    /// </summary>
    public required long WindowAggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка обработки в миллисекундах за rolling-окно.
    /// </summary>
    public required double WindowAvgDelayMs { get; init; }

    /// <summary>
    /// Максимальная задержка обработки в миллисекундах за rolling-окно.
    /// </summary>
    public required double WindowMaxDelayMs { get; init; }

    /// <summary>
    /// Скорость поступления тиков (в секунду) за rolling-окно.
    /// </summary>
    public required double WindowTickRatePerSec { get; init; }

    /// <summary>
    /// Скорость формирования агрегатов (в секунду) за rolling-окно.
    /// </summary>
    public required double WindowAggregateRatePerSec { get; init; }
}

/// <summary>
/// Статистика производительности по одному источнику.
/// </summary>
public sealed record SourceStats
{
    /// <summary>
    /// Имя источника.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Количество обработанных тиков с момента старта процесса.
    /// </summary>
    public required long TickCount { get; init; }

    /// <summary>
    /// Количество сформированных агрегатов с момента старта процесса.
    /// </summary>
    public required long AggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка обработки в миллисекундах с момента старта процесса.
    /// </summary>
    public required double AverageDelayMs { get; init; }

    /// <summary>
    /// Время последнего полученного тика.
    /// </summary>
    public required DateTimeOffset LastTickTime { get; init; }

    /// <summary>
    /// Текущий статус источника по SLA.
    /// </summary>
    public required MonitoringSourceStatus Status { get; init; }

    /// <summary>
    /// Возраст последнего тика в миллисекундах.
    /// </summary>
    public required double LastTickAgeMs { get; init; }

    /// <summary>
    /// Количество тиков за rolling-окно.
    /// </summary>
    public required long WindowTickCount { get; init; }

    /// <summary>
    /// Количество агрегатов за rolling-окно.
    /// </summary>
    public required long WindowAggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка обработки в миллисекундах за rolling-окно.
    /// </summary>
    public required double WindowAvgDelayMs { get; init; }

    /// <summary>
    /// Максимальная задержка обработки в миллисекундах за rolling-окно.
    /// </summary>
    public required double WindowMaxDelayMs { get; init; }

    /// <summary>
    /// Скорость поступления тиков (в секунду) за rolling-окно.
    /// </summary>
    public required double WindowTickRatePerSec { get; init; }

    /// <summary>
    /// Скорость формирования агрегатов (в секунду) за rolling-окно.
    /// </summary>
    public required double WindowAggregateRatePerSec { get; init; }
}

/// <summary>
/// Сводный отчет производительности за окно мониторинга.
/// </summary>
public sealed record PerformanceReport
{
    /// <summary>
    /// Длительность окна в минутах.
    /// </summary>
    public required int WindowMinutes { get; init; }

    /// <summary>
    /// Суммарное количество тиков за окно.
    /// </summary>
    public required long TotalWindowTickCount { get; init; }

    /// <summary>
    /// Суммарное количество агрегатов за окно.
    /// </summary>
    public required long TotalWindowAggregateCount { get; init; }

    /// <summary>
    /// Суммарная средняя задержка в миллисекундах за окно.
    /// </summary>
    public required double TotalWindowAvgDelayMs { get; init; }

    /// <summary>
    /// Максимальная задержка в миллисекундах за окно.
    /// </summary>
    public required double TotalWindowMaxDelayMs { get; init; }

    /// <summary>
    /// Суммарная скорость тиков (в секунду) за окно.
    /// </summary>
    public required double TotalWindowTickRatePerSec { get; init; }

    /// <summary>
    /// Суммарная скорость агрегатов (в секунду) за окно.
    /// </summary>
    public required double TotalWindowAggregateRatePerSec { get; init; }

    /// <summary>
    /// Количество источников в статусе OK.
    /// </summary>
    public required int SourcesOk { get; init; }

    /// <summary>
    /// Количество источников в статусе Warn.
    /// </summary>
    public required int SourcesWarn { get; init; }

    /// <summary>
    /// Количество источников в статусе Critical.
    /// </summary>
    public required int SourcesCritical { get; init; }
}

/// <summary>
/// Статус источника данных по SLA.
/// </summary>
public enum MonitoringSourceStatus
{
    /// <summary>
    /// Показатели источника в норме.
    /// </summary>
    Ok,

    /// <summary>
    /// Показатели источника превышают предупредительный порог.
    /// </summary>
    Warn,

    /// <summary>
    /// Показатели источника превышают критический порог.
    /// </summary>
    Critical
}

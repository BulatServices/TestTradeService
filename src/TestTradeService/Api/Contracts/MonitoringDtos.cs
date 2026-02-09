namespace TestTradeService.Api.Contracts;

/// <summary>
/// Снимок мониторинга для API.
/// </summary>
public sealed record MonitoringSnapshotDto
{
    /// <summary>
    /// Время формирования снимка.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Статистика по биржам.
    /// </summary>
    public required IReadOnlyDictionary<string, ExchangeStatsDto> ExchangeStats { get; init; }

    /// <summary>
    /// Статистика по источникам.
    /// </summary>
    public required IReadOnlyDictionary<string, SourceStatsDto> SourceStats { get; init; }

    /// <summary>
    /// Сводный отчет о производительности за окно.
    /// </summary>
    public required PerformanceReportDto PerformanceReport { get; init; }

    /// <summary>
    /// Предупреждения.
    /// </summary>
    public required IReadOnlyCollection<string> Warnings { get; init; }
}

/// <summary>
/// Статистика по бирже.
/// </summary>
public sealed record ExchangeStatsDto
{
    /// <summary>
    /// Биржа.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Количество тиков.
    /// </summary>
    public required long TickCount { get; init; }

    /// <summary>
    /// Количество агрегатов.
    /// </summary>
    public required long AggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка в миллисекундах.
    /// </summary>
    public required double AverageDelayMs { get; init; }

    /// <summary>
    /// Время последнего тика.
    /// </summary>
    public required DateTimeOffset LastTickTime { get; init; }

    /// <summary>
    /// Количество тиков за окно.
    /// </summary>
    public required long WindowTickCount { get; init; }

    /// <summary>
    /// Количество агрегатов за окно.
    /// </summary>
    public required long WindowAggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка в миллисекундах за окно.
    /// </summary>
    public required double WindowAvgDelayMs { get; init; }

    /// <summary>
    /// Максимальная задержка в миллисекундах за окно.
    /// </summary>
    public required double WindowMaxDelayMs { get; init; }

    /// <summary>
    /// Скорость поступления тиков в секунду за окно.
    /// </summary>
    public required double WindowTickRatePerSec { get; init; }

    /// <summary>
    /// Скорость формирования агрегатов в секунду за окно.
    /// </summary>
    public required double WindowAggregateRatePerSec { get; init; }
}

/// <summary>
/// Статистика по источнику.
/// </summary>
public sealed record SourceStatsDto
{
    /// <summary>
    /// Имя источника.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Количество тиков.
    /// </summary>
    public required long TickCount { get; init; }

    /// <summary>
    /// Количество агрегатов.
    /// </summary>
    public required long AggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка в миллисекундах.
    /// </summary>
    public required double AverageDelayMs { get; init; }

    /// <summary>
    /// Время последнего тика.
    /// </summary>
    public required DateTimeOffset LastTickTime { get; init; }

    /// <summary>
    /// Текущий статус источника по SLA.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Возраст последнего тика в миллисекундах.
    /// </summary>
    public required double LastTickAgeMs { get; init; }

    /// <summary>
    /// Количество тиков за окно.
    /// </summary>
    public required long WindowTickCount { get; init; }

    /// <summary>
    /// Количество агрегатов за окно.
    /// </summary>
    public required long WindowAggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка в миллисекундах за окно.
    /// </summary>
    public required double WindowAvgDelayMs { get; init; }

    /// <summary>
    /// Максимальная задержка в миллисекундах за окно.
    /// </summary>
    public required double WindowMaxDelayMs { get; init; }

    /// <summary>
    /// Скорость поступления тиков в секунду за окно.
    /// </summary>
    public required double WindowTickRatePerSec { get; init; }

    /// <summary>
    /// Скорость формирования агрегатов в секунду за окно.
    /// </summary>
    public required double WindowAggregateRatePerSec { get; init; }
}

/// <summary>
/// Сводный отчет производительности за окно.
/// </summary>
public sealed record PerformanceReportDto
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
    /// Суммарная скорость тиков в секунду за окно.
    /// </summary>
    public required double TotalWindowTickRatePerSec { get; init; }

    /// <summary>
    /// Суммарная скорость агрегатов в секунду за окно.
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

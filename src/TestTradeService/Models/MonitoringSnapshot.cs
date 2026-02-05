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
    /// Статистика по источникам.
    /// </summary>
    public required IReadOnlyDictionary<string, SourceStats> SourceStats { get; init; }

    /// <summary>
    /// Список предупреждений.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
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
    /// Количество обработанных тиков.
    /// </summary>
    public required long TickCount { get; init; }

    /// <summary>
    /// Количество сформированных агрегатов.
    /// </summary>
    public required long AggregateCount { get; init; }

    /// <summary>
    /// Средняя задержка обработки в миллисекундах.
    /// </summary>
    public required double AverageDelayMs { get; init; }

    /// <summary>
    /// Время последнего полученного тика.
    /// </summary>
    public required DateTimeOffset LastTickTime { get; init; }
}

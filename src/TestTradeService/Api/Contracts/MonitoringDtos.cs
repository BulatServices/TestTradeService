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
}

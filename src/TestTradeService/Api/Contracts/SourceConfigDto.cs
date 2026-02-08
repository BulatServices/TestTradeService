namespace TestTradeService.Api.Contracts;

/// <summary>
/// Профиль инструментов источника.
/// </summary>
public sealed record SourceProfileDto
{
    /// <summary>
    /// Биржа.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Тип рынка.
    /// </summary>
    public required string MarketType { get; init; }

    /// <summary>
    /// Транспорт источника.
    /// </summary>
    public required string Transport { get; init; }

    /// <summary>
    /// Список тикеров.
    /// </summary>
    public required IReadOnlyCollection<string> Symbols { get; init; }

    /// <summary>
    /// Интервал обновления в миллисекундах.
    /// </summary>
    public required int TargetUpdateIntervalMs { get; init; }

    /// <summary>
    /// Признак активности профиля.
    /// </summary>
    public required bool IsEnabled { get; init; }
}

/// <summary>
/// Конфигурация источников.
/// </summary>
public sealed record SourceConfigDto
{
    /// <summary>
    /// Набор профилей источников.
    /// </summary>
    public required IReadOnlyCollection<SourceProfileDto> Profiles { get; init; }
}

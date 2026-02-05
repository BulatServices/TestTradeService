namespace TestTradeService.Models;

/// <summary>
/// Текущее состояние источника данных.
/// </summary>
public sealed record SourceStatus
{
    /// <summary>
    /// Имя источника.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Признак доступности источника.
    /// </summary>
    public required bool IsOnline { get; init; }

    /// <summary>
    /// Время последнего обновления состояния.
    /// </summary>
    public required DateTimeOffset LastUpdate { get; init; }

    /// <summary>
    /// Описание ошибки или дополнительная информация.
    /// </summary>
    public string? Message { get; init; }
}

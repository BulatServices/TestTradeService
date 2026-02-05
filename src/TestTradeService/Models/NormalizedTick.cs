namespace TestTradeService.Models;

/// <summary>
/// Нормализованный тик в едином внутреннем формате.
/// </summary>
public sealed record NormalizedTick
{
    /// <summary>
    /// Источник тика.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Символ торгового инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Цена сделки.
    /// </summary>
    public required decimal Price { get; init; }

    /// <summary>
    /// Объем сделки.
    /// </summary>
    public required decimal Volume { get; init; }

    /// <summary>
    /// Время тика.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Уникальный отпечаток для дедупликации.
    /// </summary>
    public required string Fingerprint { get; init; }
}

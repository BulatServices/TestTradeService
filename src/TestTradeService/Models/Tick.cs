namespace TestTradeService.Models;

/// <summary>
/// Сырой тик, полученный от источника биржевых данных.
/// </summary>
public sealed record Tick
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
    /// Время события на бирже.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Внешний идентификатор сделки (если предоставляется источником).
    /// </summary>
    public string? TradeId { get; init; }
}

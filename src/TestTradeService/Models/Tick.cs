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

    /// <summary>
    /// Биржа, из которой получен тик, для сохранения сырого payload.
    /// </summary>
    public string? RawExchange { get; init; }

    /// <summary>
    /// Тип рынка сырого тика (например, Spot или Perp).
    /// </summary>
    public string? RawMarketType { get; init; }

    /// <summary>
    /// Время получения сырого payload.
    /// </summary>
    public DateTimeOffset? RawReceivedAt { get; init; }

    /// <summary>
    /// Исходный payload в текстовом виде.
    /// </summary>
    public string? RawPayload { get; init; }

    /// <summary>
    /// Дополнительные метаданные источника для сырого payload.
    /// </summary>
    public IReadOnlyDictionary<string, string>? RawMetadata { get; init; }
}

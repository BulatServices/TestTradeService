using System.Collections.Generic;

namespace TestTradeService.Models;

/// <summary>
/// Сырой тик, извлечённый из payload до нормализации.
/// </summary>
public sealed record RawTick
{
    /// <summary>
    /// Биржа-источник.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Символ торгового инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Тип рынка (spot, futures и т.п.).
    /// </summary>
    public required string MarketType { get; init; }

    /// <summary>
    /// Цена сделки.
    /// </summary>
    public required decimal Price { get; init; }

    /// <summary>
    /// Объём сделки.
    /// </summary>
    public required decimal Volume { get; init; }

    /// <summary>
    /// Внешний идентификатор сделки (если предоставляется источником).
    /// </summary>
    public string? TradeId { get; init; }

    /// <summary>
    /// Время события на бирже.
    /// </summary>
    public required DateTimeOffset EventTimestamp { get; init; }

    /// <summary>
    /// Локальная метка времени получения сообщения.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Исходный payload, из которого сформирован тик.
    /// </summary>
    public string? Payload { get; init; }

    /// <summary>
    /// Дополнительные метаданные источника.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

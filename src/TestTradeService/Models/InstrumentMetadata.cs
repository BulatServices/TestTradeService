namespace TestTradeService.Models;

/// <summary>
/// Метаданные торгового инструмента.
/// </summary>
public sealed record InstrumentMetadata
{
    /// <summary>
    /// Биржа, к которой относится инструмент (например, Binance).
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Тип рынка (spot/perp).
    /// </summary>
    public required MarketType MarketType { get; init; }

    /// <summary>
    /// Символ инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Базовый актив (например, BTC).
    /// </summary>
    public required string BaseAsset { get; init; }

    /// <summary>
    /// Котируемый актив (например, USDT).
    /// </summary>
    public required string QuoteAsset { get; init; }

    /// <summary>
    /// Человекочитаемое описание инструмента.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Минимальный шаг цены (tick size).
    /// </summary>
    public required decimal PriceTickSize { get; init; }

    /// <summary>
    /// Минимальный шаг объёма (lot size).
    /// </summary>
    public required decimal VolumeStep { get; init; }

    /// <summary>
    /// Точность цены.
    /// </summary>
    public required int PriceDecimals { get; init; }

    /// <summary>
    /// Точность объема.
    /// </summary>
    public required int VolumeDecimals { get; init; }

    /// <summary>
    /// Размер контракта (только для <see cref="Models.MarketType.Perp"/>); для spot обычно <c>null</c>.
    /// </summary>
    public decimal? ContractSize { get; init; }

    /// <summary>
    /// Минимальная нотиональная стоимость (если применимо для рынка/биржи); иначе <c>null</c>.
    /// </summary>
    public decimal? MinNotional { get; init; }
}

namespace TestTradeService.Models;

/// <summary>
/// Метаданные торгового инструмента.
/// </summary>
public sealed record InstrumentMetadata
{
    /// <summary>
    /// Символ инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Человекочитаемое описание инструмента.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Точность цены.
    /// </summary>
    public required int PriceDecimals { get; init; }

    /// <summary>
    /// Точность объема.
    /// </summary>
    public required int VolumeDecimals { get; init; }
}

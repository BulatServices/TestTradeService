namespace TestTradeService.Models;

/// <summary>
/// Агрегированная свеча по заданному временному окну.
/// </summary>
public sealed record AggregatedCandle
{
    /// <summary>
    /// Источник данных.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Символ инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Начало временного окна.
    /// </summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>
    /// Размер окна агрегации.
    /// </summary>
    public required TimeSpan Window { get; init; }

    /// <summary>
    /// Цена открытия.
    /// </summary>
    public required decimal Open { get; init; }

    /// <summary>
    /// Максимальная цена.
    /// </summary>
    public required decimal High { get; init; }

    /// <summary>
    /// Минимальная цена.
    /// </summary>
    public required decimal Low { get; init; }

    /// <summary>
    /// Цена закрытия.
    /// </summary>
    public required decimal Close { get; init; }

    /// <summary>
    /// Суммарный объем в окне.
    /// </summary>
    public required decimal Volume { get; init; }

    /// <summary>
    /// Количество тиков в окне.
    /// </summary>
    public required int Count { get; init; }
}

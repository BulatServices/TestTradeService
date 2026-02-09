namespace TestTradeService.Models;

/// <summary>
/// Снимок производных метрик по инструменту за окно.
/// </summary>
public sealed record MetricsSnapshot
{
    /// <summary>
    /// Символ инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Начало окна расчета.
    /// </summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>
    /// Длительность окна расчета.
    /// </summary>
    public required TimeSpan Window { get; init; }

    /// <summary>
    /// Средняя цена за окно.
    /// </summary>
    public required decimal AveragePrice { get; init; }

    /// <summary>
    /// Волатильность (стандартное отклонение цены).
    /// </summary>
    public required decimal Volatility { get; init; }

    /// <summary>
    /// Количество тиков в окне.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Средний объем сделки в окне.
    /// </summary>
    public required decimal AverageVolume { get; init; }
}

namespace TestTradeService.Api.Contracts;

/// <summary>
/// Параметры запроса свечей.
/// </summary>
public sealed record ProcessedCandlesQuery
{
    /// <summary>
    /// Фильтр по бирже.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Фильтр по символу.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Окно агрегации: 1m, 5m, 1h.
    /// </summary>
    public string? Window { get; init; }

    /// <summary>
    /// Дата начала диапазона.
    /// </summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>
    /// Дата окончания диапазона.
    /// </summary>
    public DateTimeOffset? DateTo { get; init; }

    /// <summary>
    /// Номер страницы.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Размер страницы.
    /// </summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Свеча для API.
/// </summary>
public sealed record CandleDto
{
    /// <summary>
    /// Источник данных.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Биржа.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Символ инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Начало окна.
    /// </summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>
    /// Представление окна: 1m, 5m, 1h.
    /// </summary>
    public required string Window { get; init; }

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
    /// Объем.
    /// </summary>
    public required decimal Volume { get; init; }

    /// <summary>
    /// Количество тиков.
    /// </summary>
    public required int Count { get; init; }
}

/// <summary>
/// Ответ со свечами.
/// </summary>
public sealed record CandlesResponseDto
{
    /// <summary>
    /// Общее количество элементов.
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Элементы текущей страницы.
    /// </summary>
    public required IReadOnlyCollection<CandleDto> Items { get; init; }
}

/// <summary>
/// Параметры запроса метрик.
/// </summary>
public sealed record ProcessedMetricsQuery
{
    /// <summary>
    /// Фильтр по бирже.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Фильтр по символу.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Окно агрегации: 1m, 5m, 1h.
    /// </summary>
    public string? Window { get; init; }

    /// <summary>
    /// Дата начала диапазона.
    /// </summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>
    /// Дата окончания диапазона.
    /// </summary>
    public DateTimeOffset? DateTo { get; init; }
}

/// <summary>
/// Срез метрик по инструменту.
/// </summary>
public sealed record MetricsSnapshotDto
{
    /// <summary>
    /// Символ.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Начало окна.
    /// </summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>
    /// Представление окна.
    /// </summary>
    public required string Window { get; init; }

    /// <summary>
    /// Средняя цена.
    /// </summary>
    public required decimal AveragePrice { get; init; }

    /// <summary>
    /// Волатильность.
    /// </summary>
    public required decimal Volatility { get; init; }

    /// <summary>
    /// Количество тиков.
    /// </summary>
    public required int Count { get; init; }
}

/// <summary>
/// Ответ с метриками.
/// </summary>
public sealed record MetricsResponseDto
{
    /// <summary>
    /// Набор метрик.
    /// </summary>
    public required IReadOnlyCollection<MetricsSnapshotDto> Items { get; init; }
}

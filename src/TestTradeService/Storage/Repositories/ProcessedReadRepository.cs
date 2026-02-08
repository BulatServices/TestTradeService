using Dapper;
using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;

namespace TestTradeService.Storage.Repositories;

/// <summary>
/// Репозиторий чтения обработанных данных из TimescaleDB.
/// </summary>
public sealed class ProcessedReadRepository : IProcessedReadRepository
{
    private readonly TimeseriesDataSource _timeseriesDataSource;

    /// <summary>
    /// Инициализирует репозиторий обработанных данных.
    /// </summary>
    /// <param name="timeseriesDataSource">Источник подключений к таймсериям.</param>
    public ProcessedReadRepository(TimeseriesDataSource timeseriesDataSource)
    {
        _timeseriesDataSource = timeseriesDataSource;
    }

    /// <summary>
    /// Возвращает страницу свечей по фильтрам.
    /// </summary>
    /// <param name="request">Параметры фильтрации и пагинации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пагинированный ответ со свечами.</returns>
    public async Task<CandlesResponseDto> GetCandlesAsync(ProcessedCandlesQuery request, CancellationToken cancellationToken)
    {
        var windowSeconds = ParseWindowSeconds(request.Window);
        var offset = (request.Page - 1) * request.PageSize;

        const string countSql = """
            select count(*)
            from market.candles
            where (@WindowSeconds is null or window_seconds = @WindowSeconds)
              and (@Symbol is null or symbol = @Symbol)
              and (@Exchange is null or split_part(source, '-', 1) = @Exchange)
              and (@DateFrom is null or "time" >= @DateFrom)
              and (@DateTo is null or "time" <= @DateTo);
            """;

        const string itemsSql = """
            select
                source as Source,
                coalesce(split_part(source, '-', 1), 'Unknown') as Exchange,
                symbol as Symbol,
                "time" as WindowStart,
                window_seconds as WindowSeconds,
                open as Open,
                high as High,
                low as Low,
                close as Close,
                volume as Volume,
                count as Count
            from market.candles
            where (@WindowSeconds is null or window_seconds = @WindowSeconds)
              and (@Symbol is null or symbol = @Symbol)
              and (@Exchange is null or split_part(source, '-', 1) = @Exchange)
              and (@DateFrom is null or "time" >= @DateFrom)
              and (@DateTo is null or "time" <= @DateTo)
            order by "time" desc
            offset @Offset
            limit @Limit;
            """;

        await using var connection = await _timeseriesDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        var parameters = new
        {
            WindowSeconds = windowSeconds,
            request.Symbol,
            request.Exchange,
            request.DateFrom,
            request.DateTo,
            Offset = offset,
            Limit = request.PageSize
        };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<CandleRow>(new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken))).ToArray();

        return new CandlesResponseDto
        {
            Total = total,
            Items = rows.Select(Map).ToArray()
        };
    }

    /// <summary>
    /// Возвращает срез метрик по фильтрам.
    /// </summary>
    /// <param name="request">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с метриками.</returns>
    public async Task<MetricsResponseDto> GetMetricsAsync(ProcessedMetricsQuery request, CancellationToken cancellationToken)
    {
        var windowSeconds = ParseWindowSeconds(request.Window);

        const string sql = """
            select
                symbol as Symbol,
                "time" as WindowStart,
                window_seconds as WindowSeconds,
                avg((open + high + low + close) / 4.0) as AveragePrice,
                coalesce(stddev_pop(close), 0) as Volatility,
                sum(count)::int as Count
            from market.candles
            where (@WindowSeconds is null or window_seconds = @WindowSeconds)
              and (@Symbol is null or symbol = @Symbol)
              and (@Exchange is null or split_part(source, '-', 1) = @Exchange)
              and (@DateFrom is null or "time" >= @DateFrom)
              and (@DateTo is null or "time" <= @DateTo)
            group by symbol, "time", window_seconds
            order by "time" desc
            limit 500;
            """;

        await using var connection = await _timeseriesDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<MetricsRow>(new CommandDefinition(sql, new
        {
            WindowSeconds = windowSeconds,
            request.Symbol,
            request.Exchange,
            request.DateFrom,
            request.DateTo
        }, cancellationToken: cancellationToken))).ToArray();

        return new MetricsResponseDto
        {
            Items = rows.Select(x => new MetricsSnapshotDto
            {
                Symbol = x.Symbol,
                WindowStart = x.WindowStart,
                Window = FormatWindow(x.WindowSeconds),
                AveragePrice = x.AveragePrice,
                Volatility = x.Volatility,
                Count = x.Count
            }).ToArray()
        };
    }

    private static CandleDto Map(CandleRow row)
    {
        return new CandleDto
        {
            Source = row.Source,
            Exchange = string.IsNullOrWhiteSpace(row.Exchange) ? "Unknown" : row.Exchange,
            Symbol = row.Symbol,
            WindowStart = row.WindowStart,
            Window = FormatWindow(row.WindowSeconds),
            Open = row.Open,
            High = row.High,
            Low = row.Low,
            Close = row.Close,
            Volume = row.Volume,
            Count = row.Count
        };
    }

    private static int? ParseWindowSeconds(string? window)
    {
        return window switch
        {
            null or "" => null,
            "1m" => 60,
            "5m" => 300,
            "1h" => 3600,
            _ => throw new ArgumentException("Недопустимое значение параметра window.", nameof(window))
        };
    }

    private static string FormatWindow(int seconds)
    {
        return seconds switch
        {
            60 => "1m",
            300 => "5m",
            3600 => "1h",
            _ => $"{seconds}s"
        };
    }

    private sealed record CandleRow
    {
        public required string Source { get; init; }
        public required string Exchange { get; init; }
        public required string Symbol { get; init; }
        public required DateTimeOffset WindowStart { get; init; }
        public required int WindowSeconds { get; init; }
        public required decimal Open { get; init; }
        public required decimal High { get; init; }
        public required decimal Low { get; init; }
        public required decimal Close { get; init; }
        public required decimal Volume { get; init; }
        public required int Count { get; init; }
    }

    private sealed record MetricsRow
    {
        public required string Symbol { get; init; }
        public required DateTimeOffset WindowStart { get; init; }
        public required int WindowSeconds { get; init; }
        public required decimal AveragePrice { get; init; }
        public required decimal Volatility { get; init; }
        public required int Count { get; init; }
    }
}

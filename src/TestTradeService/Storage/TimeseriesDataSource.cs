using Npgsql;

namespace TestTradeService.Storage;

/// <summary>
/// Контейнер DataSource для контура таймсерий TimescaleDB.
/// </summary>
public sealed class TimeseriesDataSource
{
    /// <summary>
    /// Инициализирует контейнер DataSource для таймсерий.
    /// </summary>
    /// <param name="dataSource">Источник подключений.</param>
    public TimeseriesDataSource(NpgsqlDataSource dataSource)
    {
        DataSource = dataSource;
    }

    /// <summary>
    /// Источник подключений для записи тиков и свечей.
    /// </summary>
    public NpgsqlDataSource DataSource { get; }
}

using Npgsql;

namespace TestTradeService.Storage;

/// <summary>
/// Контейнер DataSource для контура метаданных PostgreSQL.
/// </summary>
public sealed class MetadataDataSource
{
    /// <summary>
    /// Инициализирует контейнер DataSource для метаданных.
    /// </summary>
    /// <param name="dataSource">Источник подключений.</param>
    public MetadataDataSource(NpgsqlDataSource dataSource)
    {
        DataSource = dataSource;
    }

    /// <summary>
    /// Источник подключений для чтения/записи метаданных.
    /// </summary>
    public NpgsqlDataSource DataSource { get; }
}

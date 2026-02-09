namespace TestTradeService.Storage.Configuration;

/// <summary>
/// Настройки подключений к контурам хранения данных.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Имя секции конфигурации для настроек БД.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Строка подключения к PostgreSQL для метаданных.
    /// </summary>
    public string MetadataConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Строка подключения к TimescaleDB для таймсерий.
    /// </summary>
    public string TimeseriesConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Признак автоматического применения миграций при старте сервиса.
    /// </summary>
    public bool AutoMigrate { get; init; } = true;

    /// <summary>
    /// Проверяет, что строка подключения к БД метаданных задана.
    /// </summary>
    /// <returns><c>true</c>, если доступен контур метаданных.</returns>
    public bool HasMetadataConnection()
    {
        return !string.IsNullOrWhiteSpace(MetadataConnectionString);
    }

    /// <summary>
    /// Проверяет, что строка подключения к таймсерийной БД задана.
    /// </summary>
    /// <returns><c>true</c>, если доступен таймсерийный контур.</returns>
    public bool HasTimeseriesConnection()
    {
        return !string.IsNullOrWhiteSpace(TimeseriesConnectionString);
    }

    /// <summary>
    /// Проверяет, что обе строки подключения заданы.
    /// </summary>
    /// <returns><c>true</c>, если конфигурация позволяет включить БД-контур.</returns>
    public bool HasBothConnections()
    {
        return HasMetadataConnection() && HasTimeseriesConnection();
    }
}

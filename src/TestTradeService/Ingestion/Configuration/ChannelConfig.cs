using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Configuration;

/// <summary>
/// Конфигурация канала ingestion.
/// </summary>
public sealed class ChannelConfig
{
    /// <summary>
    /// Уникальный идентификатор канала.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Биржа-источник.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Тип канала (REST/WebSocket).
    /// </summary>
    public required ChannelKind ChannelKind { get; init; }

    /// <summary>
    /// Тип потока данных.
    /// </summary>
    public required StreamType StreamType { get; init; }

    /// <summary>
    /// Список символов.
    /// </summary>
    public required IReadOnlyCollection<string> Symbols { get; init; }

    /// <summary>
    /// Настройки REST-транспорта.
    /// </summary>
    public RestChannelSettings? Rest { get; init; }

    /// <summary>
    /// Настройки WebSocket-транспорта.
    /// </summary>
    public WebSocketChannelSettings? WebSocket { get; init; }
}

/// <summary>
/// Настройки REST polling-канала.
/// </summary>
public sealed class RestChannelSettings
{
    /// <summary>
    /// Базовый endpoint источника.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Интервал между циклами polling.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Размер батча символов на один запрос.
    /// </summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>
    /// Максимальное число повторов при ошибке.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Начальная задержка backoff.
    /// </summary>
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Ограничение по числу запросов в секунду.
    /// </summary>
    public int RequestsPerSecondLimit { get; init; } = 5;
}

/// <summary>
/// Настройки WebSocket-канала.
/// </summary>
public sealed class WebSocketChannelSettings
{
    /// <summary>
    /// Endpoint подключения.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Пауза перед переподключением.
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Интервал heartbeat/ping.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Таймаут отсутствия входящих данных.
    /// </summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

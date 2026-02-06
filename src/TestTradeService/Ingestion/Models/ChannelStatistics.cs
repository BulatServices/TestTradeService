namespace TestTradeService.Ingestion.Models;

/// <summary>
/// Снимок статистики канала.
/// </summary>
public sealed record ChannelStatistics
{
    /// <summary>
    /// Количество входящих сырых сообщений.
    /// </summary>
    public required long IncomingMessages { get; init; }

    /// <summary>
    /// Количество ошибок канала.
    /// </summary>
    public required long ErrorCount { get; init; }

    /// <summary>
    /// Количество переподключений канала.
    /// </summary>
    public required long ReconnectCount { get; init; }

    /// <summary>
    /// Время получения последнего сообщения.
    /// </summary>
    public DateTimeOffset? LastMessageAt { get; init; }

    /// <summary>
    /// Текущий сетевой статус канала.
    /// </summary>
    public required SourceConnectivityStatus Status { get; init; }
}

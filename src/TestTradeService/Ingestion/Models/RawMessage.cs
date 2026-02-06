namespace TestTradeService.Ingestion.Models;

/// <summary>
/// Сырое сообщение от источника данных без нормализации.
/// </summary>
public sealed record RawMessage
{
    /// <summary>
    /// Биржа-источник.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Идентификатор канала, опубликовавшего сообщение.
    /// </summary>
    public required string ChannelId { get; init; }

    /// <summary>
    /// Тип транспорта, через который получено сообщение.
    /// </summary>
    public required TransportType TransportType { get; init; }

    /// <summary>
    /// Локальная метка времени получения сообщения.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Исходный payload в текстовом виде.
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Дополнительные метаданные (endpoint, подписка и т.п.).
    /// </summary>
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}

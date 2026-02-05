namespace TestTradeService.Models;

/// <summary>
/// Событие алертинга, сгенерированное по правилу.
/// </summary>
public sealed record Alert
{
    /// <summary>
    /// Название правила, вызвавшего алерт.
    /// </summary>
    public required string Rule { get; init; }

    /// <summary>
    /// Источник данных.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Символ инструмента.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Текст сообщения алерта.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Время генерации алерта.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

namespace TestTradeService.Api.Contracts;

/// <summary>
/// Единый формат ошибки API.
/// </summary>
public sealed record ApiErrorResponse
{
    /// <summary>
    /// Машиночитаемый код ошибки.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Человекочитаемое сообщение об ошибке.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Дополнительные детали ошибки.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Идентификатор трассировки запроса.
    /// </summary>
    public string? TraceId { get; init; }
}

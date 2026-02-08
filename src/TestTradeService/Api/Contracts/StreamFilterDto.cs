namespace TestTradeService.Api.Contracts;

/// <summary>
/// Фильтр потока по бирже и символу.
/// </summary>
public sealed record StreamFilterDto
{
    /// <summary>
    /// Ограничение по бирже.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Ограничение по символу.
    /// </summary>
    public string? Symbol { get; init; }
}

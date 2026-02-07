namespace TestTradeService.Models;

/// <summary>
/// Конфигурация правила алертинга, загруженная из хранилища метаданных.
/// </summary>
public sealed record AlertRuleConfig
{
    /// <summary>
    /// Название правила.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Признак активности правила.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Ограничение по бирже (если <c>null</c>, правило применяется ко всем биржам).
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Ограничение по символу (если <c>null</c>, правило применяется ко всем символам).
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Набор параметров правила в формате key/value.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}

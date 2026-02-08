namespace TestTradeService.Api.Contracts;

/// <summary>
/// Параметры запроса алертов.
/// </summary>
public sealed record AlertsQuery
{
    /// <summary>
    /// Фильтр по правилу.
    /// </summary>
    public string? Rule { get; init; }

    /// <summary>
    /// Фильтр по источнику.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Фильтр по символу.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Дата начала диапазона.
    /// </summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>
    /// Дата окончания диапазона.
    /// </summary>
    public DateTimeOffset? DateTo { get; init; }
}

/// <summary>
/// Алерт для API.
/// </summary>
public sealed record AlertDto
{
    /// <summary>
    /// Правило.
    /// </summary>
    public required string Rule { get; init; }

    /// <summary>
    /// Источник.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Символ.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Сообщение.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Время события.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Конфигурация правила алертинга для API.
/// </summary>
public sealed record AlertRuleConfigDto
{
    /// <summary>
    /// Имя правила.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Признак активности.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Ограничение по бирже.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Ограничение по символу.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Параметры правила.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}

/// <summary>
/// Ответ с алертами.
/// </summary>
public sealed record AlertsResponseDto
{
    /// <summary>
    /// Список алертов.
    /// </summary>
    public required IReadOnlyCollection<AlertDto> Items { get; init; }
}

/// <summary>
/// Ответ с правилами алертинга.
/// </summary>
public sealed record AlertRulesResponseDto
{
    /// <summary>
    /// Список правил.
    /// </summary>
    public required IReadOnlyCollection<AlertRuleConfigDto> Items { get; init; }
}

/// <summary>
/// Запрос обновления правил алертинга.
/// </summary>
public sealed record PutAlertRulesRequest
{
    /// <summary>
    /// Новый список правил.
    /// </summary>
    public required IReadOnlyCollection<AlertRuleConfigDto> Items { get; init; }
}

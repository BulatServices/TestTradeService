using Dapper;
using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage.Repositories;

/// <summary>
/// Репозиторий чтения алертов и правил алертинга из PostgreSQL.
/// </summary>
public sealed class AlertReadRepository : IAlertReadRepository
{
    private readonly MetadataDataSource _metadataDataSource;

    /// <summary>
    /// Инициализирует репозиторий алертов.
    /// </summary>
    /// <param name="metadataDataSource">Источник подключений к БД метаданных.</param>
    public AlertReadRepository(MetadataDataSource metadataDataSource)
    {
        _metadataDataSource = metadataDataSource;
    }

    /// <summary>
    /// Загружает алерты по фильтрам.
    /// </summary>
    /// <param name="request">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с алертами.</returns>
    public async Task<AlertsResponseDto> GetAlertsAsync(AlertsQuery request, CancellationToken cancellationToken)
    {
        var dateFrom = request.DateFrom ?? DateTimeOffset.UtcNow.AddHours(-24);

        const string sql = """
            select
                rule as Rule,
                source as Source,
                symbol as Symbol,
                message as Message,
                "timestamp" as Timestamp
            from meta.alert_events
            where "timestamp" >= @DateFrom
              and (@DateTo is null or "timestamp" <= @DateTo)
              and (@Rule is null or rule = @Rule)
              and (@Source is null or source = @Source)
              and (@Symbol is null or symbol = @Symbol)
            order by "timestamp" desc
            limit 1000;
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<AlertRow>(new CommandDefinition(sql, new
        {
            request.Rule,
            request.Source,
            request.Symbol,
            DateFrom = dateFrom,
            request.DateTo
        }, cancellationToken: cancellationToken))).ToArray();

        return new AlertsResponseDto
        {
            Items = rows.Select(row => new AlertDto
            {
                Rule = row.Rule,
                Source = row.Source,
                Symbol = row.Symbol,
                Message = row.Message,
                Timestamp = row.Timestamp
            }).ToArray()
        };
    }

    /// <summary>
    /// Загружает правила алертинга.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с правилами.</returns>
    public async Task<AlertRulesResponseDto> GetAlertRulesAsync(CancellationToken cancellationToken)
    {
        var repository = new PostgresConfigurationRepository(_metadataDataSource);
        var rules = await repository.GetAlertRulesAsync(cancellationToken);
        var globalChannels = ExtractGlobalChannels(rules);

        return new AlertRulesResponseDto
        {
            Items = rules.Where(x => !IsGlobalConfig(x)).Select(Map).ToArray(),
            GlobalChannels = globalChannels
        };
    }

    /// <summary>
    /// Преобразует доменную модель правила в DTO API.
    /// </summary>
    /// <param name="item">Конфигурация правила.</param>
    /// <returns>DTO правила алертинга.</returns>
    public static AlertRuleConfigDto Map(AlertRuleConfig item)
    {
        return new AlertRuleConfigDto
        {
            RuleName = item.RuleName,
            Enabled = item.Enabled,
            Exchange = item.Exchange,
            Symbol = item.Symbol,
            Parameters = item.Parameters
        };
    }

    private static IReadOnlyCollection<string> ExtractGlobalChannels(IEnumerable<AlertRuleConfig> items)
    {
        var globalConfig = items.FirstOrDefault(IsGlobalConfig);
        if (globalConfig is null)
        {
            return Array.Empty<string>();
        }

        return AlertingChannels.ParseCsv(globalConfig.Parameters.GetValueOrDefault(AlertingChannels.ChannelsParameterKey));
    }

    private static bool IsGlobalConfig(AlertRuleConfig item)
    {
        return string.Equals(item.RuleName, AlertingChannels.GlobalRuleName, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AlertRow
    {
        public required string Rule { get; init; }
        public required string Source { get; init; }
        public required string Symbol { get; init; }
        public required string Message { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }
}

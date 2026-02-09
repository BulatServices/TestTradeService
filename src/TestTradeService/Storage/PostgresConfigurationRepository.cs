using Dapper;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage;

/// <summary>
/// Репозиторий конфигурации торговой системы в PostgreSQL.
/// </summary>
public sealed class PostgresConfigurationRepository : IConfigurationRepository
{
    private readonly MetadataDataSource _metadataDataSource;

    /// <summary>
    /// Инициализирует репозиторий конфигурации.
    /// </summary>
    /// <param name="metadataDataSource">Источник подключений к PostgreSQL для метаданных.</param>
    public PostgresConfigurationRepository(MetadataDataSource metadataDataSource)
    {
        _metadataDataSource = metadataDataSource;
    }

    /// <summary>
    /// Загружает конфигурацию инструментов по биржам и типам рынков.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Конфигурация инструментов.</returns>
    public async Task<MarketInstrumentsConfig> GetMarketInstrumentsConfigAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            select
                exchange,
                market_type as MarketType,
                transport as Transport,
                symbol,
                target_update_interval_ms as TargetUpdateIntervalMs
            from meta.instruments
            where is_active = true
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<InstrumentConfigRow>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();
        var profiles = rows
            .GroupBy(r => $"{r.Exchange}::{r.MarketType}::{r.Transport}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                if (!Enum.TryParse<MarketExchange>(first.Exchange, true, out var exchange))
                {
                    return null;
                }

                return new MarketInstrumentProfile
                {
                    Exchange = exchange,
                    MarketType = Enum.Parse<MarketType>(first.MarketType, true),
                    Transport = Enum.Parse<MarketDataSourceTransport>(first.Transport, true),
                    Symbols = group.Select(x => x.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    TargetUpdateInterval = TimeSpan.FromMilliseconds(group.Max(x => x.TargetUpdateIntervalMs))
                };
            })
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .ToArray();

        return new MarketInstrumentsConfig
        {
            Profiles = profiles
        };
    }

    /// <summary>
    /// Загружает конфигурацию правил алертинга с параметрами.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор конфигураций правил.</returns>
    public async Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken)
    {
        const string sqlDefinitions = """
            select id, rule_name as RuleName, exchange, symbol, is_enabled as Enabled
            from meta.alert_rule_definitions
            order by lower(rule_name), coalesce(lower(exchange), ''), coalesce(lower(symbol), ''), id
            """;

        const string sqlParameters = """
            select rule_definition_id as RuleDefinitionId, param_key as ParamKey, param_value as ParamValue
            from meta.alert_rule_parameters
            order by rule_definition_id, lower(param_key), param_value
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);

        var definitions = (await connection.QueryAsync<AlertRuleDefinitionRow>(
            new CommandDefinition(sqlDefinitions, cancellationToken: cancellationToken))).ToArray();
        var parameters = (await connection.QueryAsync<AlertRuleParameterRow>(
            new CommandDefinition(sqlParameters, cancellationToken: cancellationToken))).ToArray();

        var parametersByDefinition = parameters
            .GroupBy(x => x.RuleDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, string>)group.ToDictionary(
                    x => x.ParamKey,
                    x => x.ParamValue,
                    StringComparer.OrdinalIgnoreCase));

        return definitions
            .Select(definition => new AlertRuleConfig
            {
                RuleName = definition.RuleName,
                Enabled = definition.Enabled,
                Exchange = definition.Exchange,
                Symbol = definition.Symbol,
                Parameters = parametersByDefinition.GetValueOrDefault(
                    definition.Id,
                    new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase))
            })
            .ToArray();
    }

    private sealed record InstrumentConfigRow
    {
        public required string Exchange { get; init; }
        public required string MarketType { get; init; }
        public required string Transport { get; init; }
        public required string Symbol { get; init; }
        public required int TargetUpdateIntervalMs { get; init; }
    }

    private sealed record AlertRuleDefinitionRow
    {
        public required long Id { get; init; }
        public required string RuleName { get; init; }
        public string? Exchange { get; init; }
        public string? Symbol { get; init; }
        public required bool Enabled { get; init; }
    }

    private sealed record AlertRuleParameterRow
    {
        public required long RuleDefinitionId { get; init; }
        public required string ParamKey { get; init; }
        public required string ParamValue { get; init; }
    }
}

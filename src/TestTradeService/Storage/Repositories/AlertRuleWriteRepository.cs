using Dapper;
using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage.Repositories;

/// <summary>
/// Репозиторий сохранения правил алертинга в PostgreSQL.
/// </summary>
public sealed class AlertRuleWriteRepository : IAlertRuleWriteRepository
{
    private readonly MetadataDataSource _metadataDataSource;
    private readonly IMutableAlertRuleConfigProvider _configProvider;

    /// <summary>
    /// Инициализирует репозиторий записи правил алертинга.
    /// </summary>
    /// <param name="metadataDataSource">Источник подключений к БД метаданных.</param>
    /// <param name="configProvider">Провайдер runtime-конфигурации правил.</param>
    public AlertRuleWriteRepository(MetadataDataSource metadataDataSource, IMutableAlertRuleConfigProvider configProvider)
    {
        _metadataDataSource = metadataDataSource;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Синхронизирует правила алертинга в хранилище.
    /// </summary>
    /// <param name="request">Новый снимок правил.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения правил.</returns>
    public async Task SaveAlertRulesAsync(PutAlertRulesRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string deleteSql = "delete from meta.alert_rule_definitions;";
        await connection.ExecuteAsync(new CommandDefinition(deleteSql, transaction: transaction, cancellationToken: cancellationToken));

        const string insertDefinitionSql = """
            insert into meta.alert_rule_definitions(rule_name, exchange, symbol, is_enabled)
            values (@RuleName, @Exchange, @Symbol, @Enabled)
            returning id;
            """;

        const string insertParameterSql = """
            insert into meta.alert_rule_parameters(rule_definition_id, param_key, param_value)
            values (@RuleDefinitionId, @ParamKey, @ParamValue);
            """;

        foreach (var item in request.Items)
        {
            var definitionId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(insertDefinitionSql, item, transaction: transaction, cancellationToken: cancellationToken));

            foreach (var pair in item.Parameters)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertParameterSql, new
                {
                    RuleDefinitionId = definitionId,
                    ParamKey = pair.Key,
                    ParamValue = pair.Value
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        _configProvider.Update(request.Items.Select(item => new AlertRuleConfig
        {
            RuleName = item.RuleName,
            Enabled = item.Enabled,
            Exchange = item.Exchange,
            Symbol = item.Symbol,
            Parameters = new Dictionary<string, string>(item.Parameters, StringComparer.OrdinalIgnoreCase)
        }).ToArray());
    }
}

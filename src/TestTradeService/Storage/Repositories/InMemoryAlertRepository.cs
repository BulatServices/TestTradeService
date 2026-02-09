using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage.Repositories;

/// <summary>
/// In-memory реализация чтения/записи алертов при отключенной БД.
/// </summary>
public sealed class InMemoryAlertRepository : IAlertReadRepository, IAlertRuleWriteRepository
{
    private readonly IMutableAlertRuleConfigProvider _configProvider;

    /// <summary>
    /// Инициализирует in-memory репозиторий алертов.
    /// </summary>
    /// <param name="configProvider">Провайдер runtime-конфигурации правил.</param>
    public InMemoryAlertRepository(IMutableAlertRuleConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    /// <summary>
    /// Возвращает пустой список алертов.
    /// </summary>
    /// <param name="request">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ с алертами.</returns>
    public Task<AlertsResponseDto> GetAlertsAsync(AlertsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AlertsResponseDto
        {
            Items = Array.Empty<AlertDto>()
        });
    }

    /// <summary>
    /// Возвращает текущие правила из runtime-провайдера.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с правилами.</returns>
    public Task<AlertRulesResponseDto> GetAlertRulesAsync(CancellationToken cancellationToken)
    {
        var rules = _configProvider.GetAll();
        var globalChannels = rules
            .Where(item => string.Equals(item.RuleName, AlertingChannels.GlobalRuleName, StringComparison.OrdinalIgnoreCase))
            .Select(item => AlertingChannels.ParseCsv(item.Parameters.GetValueOrDefault(AlertingChannels.ChannelsParameterKey)))
            .FirstOrDefault() ?? Array.Empty<string>();

        var items = rules
            .Where(item => !string.Equals(item.RuleName, AlertingChannels.GlobalRuleName, StringComparison.OrdinalIgnoreCase))
            .Select(AlertReadRepository.Map)
            .ToArray();

        return Task.FromResult(new AlertRulesResponseDto
        {
            Items = items,
            GlobalChannels = globalChannels
        });
    }

    /// <summary>
    /// Обновляет runtime-снимок правил алертинга.
    /// </summary>
    /// <param name="request">Новый список правил.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача обновления правил.</returns>
    public Task SaveAlertRulesAsync(PutAlertRulesRequest request, CancellationToken cancellationToken)
    {
        var globalChannels = AlertingChannels.Normalize(request.GlobalChannels ?? Array.Empty<string>());
        var ruleItems = request.Items
            .Where(item => !string.Equals(item.RuleName, AlertingChannels.GlobalRuleName, StringComparison.OrdinalIgnoreCase));

        var updatedItems = ruleItems.Select(item => new AlertRuleConfig
        {
            RuleName = item.RuleName,
            Enabled = item.Enabled,
            Exchange = item.Exchange,
            Symbol = item.Symbol,
            Parameters = new Dictionary<string, string>(item.Parameters, StringComparer.OrdinalIgnoreCase)
        }).ToList();

        updatedItems.Add(new AlertRuleConfig
        {
            RuleName = AlertingChannels.GlobalRuleName,
            Enabled = true,
            Exchange = null,
            Symbol = null,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AlertingChannels.ChannelsParameterKey] = AlertingChannels.ToCsv(globalChannels)
            }
        });

        _configProvider.Update(updatedItems.ToArray());

        return Task.CompletedTask;
    }
}

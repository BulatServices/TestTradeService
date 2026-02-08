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
        var items = _configProvider.GetAll()
            .Select(AlertReadRepository.Map)
            .ToArray();

        return Task.FromResult(new AlertRulesResponseDto
        {
            Items = items
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
        _configProvider.Update(request.Items.Select(item => new AlertRuleConfig
        {
            RuleName = item.RuleName,
            Enabled = item.Enabled,
            Exchange = item.Exchange,
            Symbol = item.Symbol,
            Parameters = new Dictionary<string, string>(item.Parameters, StringComparer.OrdinalIgnoreCase)
        }).ToArray());

        return Task.CompletedTask;
    }
}

using TestTradeService.Api.Contracts;

namespace TestTradeService.Interfaces;

/// <summary>
/// Репозиторий записи правил алертинга.
/// </summary>
public interface IAlertRuleWriteRepository
{
    /// <summary>
    /// Синхронизирует правила алертинга в хранилище.
    /// </summary>
    /// <param name="request">Новый снимок правил.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения правил.</returns>
    Task SaveAlertRulesAsync(PutAlertRulesRequest request, CancellationToken cancellationToken);
}

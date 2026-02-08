using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Мутируемый провайдер параметров правил алертинга.
/// </summary>
public interface IMutableAlertRuleConfigProvider : IAlertRuleConfigProvider
{
    /// <summary>
    /// Заменяет текущий снимок конфигураций правил.
    /// </summary>
    /// <param name="configs">Новый набор конфигураций правил.</param>
    void Update(IReadOnlyCollection<AlertRuleConfig> configs);
}

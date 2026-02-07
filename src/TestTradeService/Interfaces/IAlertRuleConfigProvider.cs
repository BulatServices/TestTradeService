using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Провайдер параметров правил алертинга.
/// </summary>
public interface IAlertRuleConfigProvider
{
    /// <summary>
    /// Возвращает параметры правила для указанного источника и символа.
    /// </summary>
    /// <param name="ruleName">Имя правила.</param>
    /// <param name="source">Имя источника данных.</param>
    /// <param name="symbol">Символ инструмента.</param>
    /// <returns>Параметры правила; пустой набор, если правило не настроено.</returns>
    IReadOnlyDictionary<string, string> GetParameters(string ruleName, string source, string symbol);

    /// <summary>
    /// Возвращает снимок всех конфигураций правил.
    /// </summary>
    /// <returns>Набор конфигураций правил.</returns>
    IReadOnlyCollection<AlertRuleConfig> GetAll();
}

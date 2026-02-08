using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage;

/// <summary>
/// In-memory провайдер параметров правил алертинга.
/// </summary>
public sealed class AlertRuleConfigProvider : IMutableAlertRuleConfigProvider
{
    private IReadOnlyCollection<AlertRuleConfig> _configs;
    private readonly object _sync = new();

    /// <summary>
    /// Инициализирует провайдер параметров правил.
    /// </summary>
    /// <param name="configs">Конфигурации правил.</param>
    public AlertRuleConfigProvider(IReadOnlyCollection<AlertRuleConfig> configs)
    {
        _configs = configs;
    }

    /// <summary>
    /// Возвращает параметры правила для указанного источника и символа.
    /// </summary>
    /// <param name="ruleName">Имя правила.</param>
    /// <param name="source">Имя источника данных.</param>
    /// <param name="symbol">Символ инструмента.</param>
    /// <returns>Параметры правила; пустой набор, если правило отключено или не найдено.</returns>
    public IReadOnlyDictionary<string, string> GetParameters(string ruleName, string source, string symbol)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
            return Empty();

        var exchange = ExtractExchange(source);
        IReadOnlyCollection<AlertRuleConfig> snapshot;
        lock (_sync)
        {
            snapshot = _configs;
        }

        var best = snapshot
            .Where(c => c.Enabled && string.Equals(c.RuleName, ruleName, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Exchange is null || string.Equals(c.Exchange, exchange, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Symbol is null || string.Equals(c.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => Score(c.Exchange, c.Symbol))
            .FirstOrDefault();

        return best?.Parameters ?? Empty();
    }

    /// <summary>
    /// Возвращает снимок всех конфигураций правил.
    /// </summary>
    /// <returns>Набор конфигураций правил.</returns>
    public IReadOnlyCollection<AlertRuleConfig> GetAll()
    {
        lock (_sync)
        {
            return _configs.ToArray();
        }
    }

    /// <summary>
    /// Заменяет текущий набор конфигураций правил.
    /// </summary>
    /// <param name="configs">Новый снимок конфигураций правил.</param>
    public void Update(IReadOnlyCollection<AlertRuleConfig> configs)
    {
        lock (_sync)
        {
            _configs = configs;
        }
    }

    private static int Score(string? exchange, string? symbol)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(exchange))
            score += 10;
        if (!string.IsNullOrWhiteSpace(symbol))
            score += 100;

        return score;
    }

    private static string? ExtractExchange(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        var separatorIndex = source.IndexOf('-');
        if (separatorIndex <= 0)
            return source;

        return source[..separatorIndex];
    }

    private static IReadOnlyDictionary<string, string> Empty() => new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);
}

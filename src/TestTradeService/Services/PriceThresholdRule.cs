using System.Globalization;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Правило алертинга по выходу цены за заданные пороги.
/// </summary>
public sealed class PriceThresholdRule : IAlertRule
{
    private readonly IAlertRuleConfigProvider _configProvider;

    /// <summary>
    /// Инициализирует правило контроля ценовых порогов.
    /// </summary>
    /// <param name="configProvider">Провайдер параметров правил алертинга.</param>
    public PriceThresholdRule(IAlertRuleConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    /// <summary>
    /// Имя правила.
    /// </summary>
    public string Name => "PriceThreshold";

    /// <summary>
    /// Проверяет превышение/понижение цены относительно порогов.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="metrics">Рассчитанные метрики по инструменту.</param>
    /// <returns><c>true</c>, если цена вышла за допустимый диапазон.</returns>
    public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics)
    {
        var parameters = _configProvider.GetParameters(Name, tick.Source, tick.Symbol);
        if (!TryGetDecimal(parameters, "min_price", out var minPrice) ||
            !TryGetDecimal(parameters, "max_price", out var maxPrice))
        {
            return false;
        }

        return tick.Price > maxPrice || tick.Price < minPrice;
    }

    /// <summary>
    /// Создает алерт по факту срабатывания правила.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="metrics">Рассчитанные метрики.</param>
    /// <returns>Сформированный алерт.</returns>
    public Alert CreateAlert(NormalizedTick tick, MetricsSnapshot metrics)
    {
        return new Alert
        {
            Rule = Name,
            Source = tick.Source,
            Symbol = tick.Symbol,
            Message = $"Price threshold breached for {tick.Source}/{tick.Symbol}: {tick.Price}",
            Timestamp = tick.Timestamp
        };
    }

    private static bool TryGetDecimal(IReadOnlyDictionary<string, string> parameters, string key, out decimal parsed)
    {
        parsed = default;
        if (!parameters.TryGetValue(key, out var value))
            return false;

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }
}

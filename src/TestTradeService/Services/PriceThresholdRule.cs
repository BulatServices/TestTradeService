using System.Globalization;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// ѕравило алертинга по выходу цены за заданные пороги.
/// </summary>
public sealed class PriceThresholdRule : IAlertRule
{
    private const decimal DefaultMinPrice = 18_000m;
    private const decimal DefaultMaxPrice = 22_000m;
    private readonly IAlertRuleConfigProvider _configProvider;

    /// <summary>
    /// »нициализирует правило контрол€ ценовых порогов.
    /// </summary>
    /// <param name="configProvider">ѕровайдер параметров правил алертинга.</param>
    public PriceThresholdRule(IAlertRuleConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    /// <summary>
    /// »м€ правила.
    /// </summary>
    public string Name => "PriceThreshold";

    /// <summary>
    /// ѕровер€ет превышение/понижение цены относительно порогов.
    /// </summary>
    /// <param name="tick">Ќормализованный тик.</param>
    /// <param name="metrics">–ассчитанные метрики по инструменту.</param>
    /// <returns><c>true</c>, если цена вышла за допустимый диапазон.</returns>
    public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics)
    {
        var parameters = _configProvider.GetParameters(Name, tick.Source, tick.Symbol);
        var minPrice = GetDecimal(parameters, "min_price", DefaultMinPrice);
        var maxPrice = GetDecimal(parameters, "max_price", DefaultMaxPrice);

        return tick.Price > maxPrice || tick.Price < minPrice;
    }

    /// <summary>
    /// —оздает алерт по факту срабатывани€ правила.
    /// </summary>
    /// <param name="tick">Ќормализованный тик.</param>
    /// <param name="metrics">–ассчитанные метрики.</param>
    /// <returns>—формированный алерт.</returns>
    public Alert CreateAlert(NormalizedTick tick, MetricsSnapshot metrics)
    {
        return new Alert
        {
            Rule = Name,
            Source = tick.Source,
            Symbol = tick.Symbol,
            Message = $"Price threshold breached: {tick.Price}",
            Timestamp = tick.Timestamp
        };
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, string> parameters, string key, decimal fallback)
    {
        if (!parameters.TryGetValue(key, out var value))
            return fallback;

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}

using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Правило алертинга по выходу цены за заданные пороги.
/// </summary>
public sealed class PriceThresholdRule : IAlertRule
{
    /// <summary>
    /// Имя правила.
    /// </summary>
    public string Name => "PriceThreshold";

    /// <summary>
    /// Проверяет превышение/понижение цены относительно порогов.
    /// </summary>
    public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics)
    {
        return tick.Price > 22_000m || tick.Price < 18_000m;
    }

    /// <summary>
    /// Создает алерт по факту срабатывания правила.
    /// </summary>
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
}

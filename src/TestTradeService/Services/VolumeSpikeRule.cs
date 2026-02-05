using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Правило алертинга на резкий всплеск объема.
/// </summary>
public sealed class VolumeSpikeRule : IAlertRule
{
    /// <summary>
    /// Имя правила.
    /// </summary>
    public string Name => "VolumeSpike";

    /// <summary>
    /// Проверяет условия всплеска объема.
    /// </summary>
    public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics)
    {
        return tick.Volume > 4m && metrics.Count > 5;
    }

    /// <summary>
    /// Формирует алерт по всплеску объема.
    /// </summary>
    public Alert CreateAlert(NormalizedTick tick, MetricsSnapshot metrics)
    {
        return new Alert
        {
            Rule = Name,
            Source = tick.Source,
            Symbol = tick.Symbol,
            Message = $"Volume spike detected: {tick.Volume}",
            Timestamp = tick.Timestamp
        };
    }
}

using System.Globalization;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Правило алертинга на резкий всплеск объема.
/// </summary>
public sealed class VolumeSpikeRule : IAlertRule
{
    private const decimal DefaultMinVolume = 4m;
    private const int DefaultMinCount = 5;
    private readonly IAlertRuleConfigProvider _configProvider;

    /// <summary>
    /// Инициализирует правило контроля всплеска объема.
    /// </summary>
    /// <param name="configProvider">Провайдер параметров правил алертинга.</param>
    public VolumeSpikeRule(IAlertRuleConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    /// <summary>
    /// Имя правила.
    /// </summary>
    public string Name => "VolumeSpike";

    /// <summary>
    /// Проверяет условия всплеска объема.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="metrics">Рассчитанные метрики по инструменту.</param>
    /// <returns><c>true</c>, если правило сработало.</returns>
    public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics)
    {
        var parameters = _configProvider.GetParameters(Name, tick.Source, tick.Symbol);
        var minVolume = GetDecimal(parameters, "min_volume", DefaultMinVolume);
        var minCount = GetInt(parameters, "min_count", DefaultMinCount);

        return tick.Volume > minVolume && metrics.Count > minCount;
    }

    /// <summary>
    /// Формирует алерт по всплеску объема.
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
            Message = $"Volume spike detected: {tick.Volume}",
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

    private static int GetInt(IReadOnlyDictionary<string, string> parameters, string key, int fallback)
    {
        if (!parameters.TryGetValue(key, out var value))
            return fallback;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}

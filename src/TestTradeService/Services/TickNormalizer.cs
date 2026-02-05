using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Приводит входные тики к единому внутреннему формату.
/// </summary>
public sealed class TickNormalizer
{
    /// <summary>
    /// Нормализует сырой тик и формирует fingerprint для дедупликации.
    /// </summary>
    /// <param name="tick">Входной сырой тик.</param>
    /// <returns>Нормализованный тик.</returns>
    public NormalizedTick Normalize(Tick tick)
    {
        var fingerprint = $"{tick.Source}:{tick.Symbol}:{tick.Timestamp:O}:{tick.Price}:{tick.Volume}:{tick.TradeId}";
        return new NormalizedTick
        {
            Source = tick.Source,
            Symbol = tick.Symbol,
            Price = tick.Price,
            Volume = tick.Volume,
            Timestamp = tick.Timestamp,
            Fingerprint = fingerprint
        };
    }
}

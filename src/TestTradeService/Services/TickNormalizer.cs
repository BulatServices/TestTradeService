using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Приводит формализованные входные тики к единому внутреннему формату.
/// </summary>
public sealed class TickNormalizer
{
    /// <summary>
    /// Нормализует тик, полученный после парсинга raw payload, и формирует fingerprint для дедупликации.
    /// </summary>
    /// <param name="tick">Формализованный тик, полученный из raw payload.</param>
    /// <returns>Нормализованный тик.</returns>
    public NormalizedTick Normalize(Tick tick)
    {
        var fingerprint = TickFingerprint.Build(
            tick.Source,
            tick.Symbol,
            tick.Timestamp,
            tick.Price,
            tick.Volume,
            tick.TradeId);
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

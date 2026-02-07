using TestTradeService.Models;

namespace TestTradeService.Ingestion.Parsing;

/// <summary>
/// Преобразует сырые тики в внутренний формат Tick.
/// </summary>
public sealed class RawTickMapper
{
    /// <summary>
    /// Преобразует сырой тик в формат Tick для нормализации.
    /// </summary>
    /// <param name="rawTick">Сырой тик после парсинга payload.</param>
    /// <returns>Сформированный тик.</returns>
    /// <exception cref="ArgumentNullException">Сырой тик не передан.</exception>
    public Tick MapToTick(RawTick rawTick)
    {
        if (rawTick is null)
            throw new ArgumentNullException(nameof(rawTick));

        return new Tick
        {
            Source = rawTick.Exchange,
            Symbol = rawTick.Symbol,
            Price = rawTick.Price,
            Volume = rawTick.Volume,
            Timestamp = rawTick.EventTimestamp,
            TradeId = rawTick.TradeId
        };
    }
}

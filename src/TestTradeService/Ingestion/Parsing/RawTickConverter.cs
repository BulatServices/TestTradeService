using TestTradeService.Ingestion.Models;
using TestTradeService.Models;

namespace TestTradeService.Ingestion.Parsing;

/// <summary>
/// Конвертирует raw payload из канала в тик доменной модели.
/// </summary>
public sealed class RawTickConverter
{
    private readonly RawTickParser _parser = new();
    private readonly RawTickMapper _mapper = new();

    /// <summary>
    /// Пытается преобразовать raw payload в тик.
    /// Обязательные поля payload: symbol, marketType, price, volume.
    /// Поле exchange берётся из payload или RawMessage.Exchange, а eventTimestamp — из payload либо из RawMessage.ReceivedAt.
    /// </summary>
    /// <param name="message">Сообщение с raw payload.</param>
    /// <param name="tick">Сформированный тик, если преобразование успешно.</param>
    /// <param name="error">Описание ошибки при неудачном преобразовании.</param>
    /// <returns>true, если тик успешно сформирован; иначе false.</returns>
    public bool TryConvert(RawMessage message, out Tick? tick, out string? error)
    {
        tick = null;
        error = null;

        if (!_parser.TryParse(message, out var rawTick, out error) || rawTick is null)
            return false;

        tick = _mapper.MapToTick(rawTick);
        return true;
    }
}

using System.Globalization;
using System.Text.Json;
using TestTradeService.Ingestion.Models;
using TestTradeService.Models;

namespace TestTradeService.Ingestion.Parsing;

/// <summary>
/// Разбирает сырые payload-сообщения в структурированный сырой тик.
/// </summary>
public sealed class RawTickParser
{
    /// <summary>
    /// Пытается извлечь сырой тик из сообщения канала.
    /// Обязательные поля payload: symbol, marketType, price, volume.
    /// Поле exchange берётся из payload или RawMessage.Exchange, а eventTimestamp — из payload либо из RawMessage.ReceivedAt.
    /// </summary>
    /// <param name="message">Сообщение канала с payload.</param>
    /// <param name="rawTick">Результат парсинга при успехе.</param>
    /// <param name="error">Описание ошибки, если парсинг не удался.</param>
    /// <returns>true, если тик разобран успешно; иначе false.</returns>
    public bool TryParse(RawMessage message, out RawTick? rawTick, out string? error)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        rawTick = null;
        error = null;

        if (string.IsNullOrWhiteSpace(message.Payload))
        {
            error = "Payload пустой или отсутствует.";
            return false;
        }

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(message.Payload);
        }
        catch (JsonException exception)
        {
            error = $"Payload не является корректным JSON: {exception.Message}";
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Payload должен быть JSON-объектом.";
                return false;
            }

            var root = document.RootElement;
            var exchange = GetString(root, "exchange") ?? message.Exchange;
            var symbol = GetString(root, "symbol");
            var marketType = GetString(root, "marketType");
            var price = GetDecimal(root, "price");
            var volume = GetDecimal(root, "volume");
            var tradeId = GetString(root, "tradeId");
            var eventTimestamp = GetDateTimeOffset(root, "eventTimestamp") ?? message.ReceivedAt;

            if (string.IsNullOrWhiteSpace(exchange))
            {
                error = "Поле exchange обязательно (может браться из RawMessage.Exchange).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(symbol))
            {
                error = "Поле symbol обязательно в payload.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(marketType))
            {
                error = "Поле marketType обязательно в payload.";
                return false;
            }

            if (!price.HasValue)
            {
                error = "Поле price обязательно и должно быть числом.";
                return false;
            }

            if (!volume.HasValue)
            {
                error = "Поле volume обязательно и должно быть числом.";
                return false;
            }

            rawTick = new RawTick
            {
                Exchange = exchange,
                Source = message.ChannelId,
                Symbol = symbol,
                MarketType = marketType,
                Price = price.Value,
                Volume = volume.Value,
                TradeId = tradeId,
                EventTimestamp = eventTimestamp,
                ReceivedAt = message.ReceivedAt,
                Payload = message.Payload,
                Metadata = message.Metadata
            };

            return true;
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var numeric))
            return numeric;

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixMillis))
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMillis);

        return null;
    }
}

using System.Globalization;
using System.Text.Json;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Kraken;

/// <summary>
/// Парсеры payload для потока сделок Kraken (WebSocket и REST).
/// </summary>
public static class KrakenTradesParsing
{
    /// <summary>
    /// Пытается распарсить WebSocket payload Kraken и вернуть 0..N тиков сделок.
    /// </summary>
    /// <param name="payload">Сырой JSON payload.</param>
    /// <param name="receivedAt">Локальная метка времени получения сообщения.</param>
    /// <returns>Список тиков (может быть пустым).</returns>
    public static IReadOnlyCollection<Tick> ParseWebSocketTrades(string payload, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<Tick>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<Tick>();

            var root = document.RootElement;
            if (root.GetArrayLength() < 4)
                return Array.Empty<Tick>();

            var streamType = root[2].ValueKind == JsonValueKind.String ? root[2].GetString() : null;
            if (!string.Equals(streamType, "trade", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<Tick>();

            var symbol = root[3].ValueKind == JsonValueKind.String ? root[3].GetString() : null;
            if (string.IsNullOrWhiteSpace(symbol))
                return Array.Empty<Tick>();

            if (root[1].ValueKind != JsonValueKind.Array)
                return Array.Empty<Tick>();

            var trades = new List<Tick>();
            foreach (var trade in root[1].EnumerateArray())
            {
                if (trade.ValueKind != JsonValueKind.Array)
                    continue;

                var fields = trade.EnumerateArray().ToArray();
                if (fields.Length < 4)
                    continue;

                var price = GetDecimal(fields[0]);
                var volume = GetDecimal(fields[1]);
                var timeSecondsRaw = fields[2].ValueKind == JsonValueKind.String ? fields[2].GetString() : fields[2].GetRawText();
                var side = fields[3].ValueKind == JsonValueKind.String ? fields[3].GetString() : null;

                if (!price.HasValue || !volume.HasValue || string.IsNullOrWhiteSpace(timeSecondsRaw))
                    continue;

                var timestamp = TryParseUnixSeconds(timeSecondsRaw) ?? receivedAt;
                var tradeId = $"{symbol}:{timeSecondsRaw}:{price.Value.ToString(CultureInfo.InvariantCulture)}:{volume.Value.ToString(CultureInfo.InvariantCulture)}:{side}";

                trades.Add(new Tick
                {
                    Source = MarketExchange.Kraken.ToString(),
                    Symbol = symbol!,
                    Price = price.Value,
                    Volume = volume.Value,
                    Timestamp = timestamp,
                    TradeId = tradeId
                });
            }

            return trades;
        }
        catch (JsonException)
        {
            return Array.Empty<Tick>();
        }
    }

    /// <summary>
    /// Пытается распарсить REST ответ Kraken Trades и вернуть 0..N тиков сделок, а также курсор <c>last</c>.
    /// </summary>
    /// <param name="symbol">Символ в формате конфигурации (например, <c>XBT/USD</c>).</param>
    /// <param name="payload">Сырой JSON payload.</param>
    /// <param name="last">Курсор <c>last</c>, если он найден в ответе.</param>
    /// <returns>Список тиков (может быть пустым).</returns>
    public static IReadOnlyCollection<Tick> ParseRestTrades(string symbol, string payload, out string? last)
    {
        last = null;
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<Tick>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<Tick>();

            if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                return Array.Empty<Tick>();

            if (result.TryGetProperty("last", out var lastProp))
                last = lastProp.GetString() ?? lastProp.GetRawText();

            JsonElement? tradesArray = null;
            foreach (var property in result.EnumerateObject())
            {
                if (string.Equals(property.Name, "last", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    tradesArray = property.Value;
                    break;
                }
            }

            if (tradesArray is null)
                return Array.Empty<Tick>();

            var ticks = new List<Tick>();
            foreach (var trade in tradesArray.Value.EnumerateArray())
            {
                if (trade.ValueKind != JsonValueKind.Array)
                    continue;

                var fields = trade.EnumerateArray().ToArray();
                if (fields.Length < 4)
                    continue;

                var price = GetDecimal(fields[0]);
                var volume = GetDecimal(fields[1]);
                var timeSecondsRaw = fields[2].ValueKind == JsonValueKind.String ? fields[2].GetString() : fields[2].GetRawText();
                var side = fields[3].ValueKind == JsonValueKind.String ? fields[3].GetString() : null;

                if (!price.HasValue || !volume.HasValue || string.IsNullOrWhiteSpace(timeSecondsRaw))
                    continue;

                var timestamp = TryParseUnixSeconds(timeSecondsRaw) ?? DateTimeOffset.UtcNow;
                var tradeId = $"{symbol}:{timeSecondsRaw}:{price.Value.ToString(CultureInfo.InvariantCulture)}:{volume.Value.ToString(CultureInfo.InvariantCulture)}:{side}";

                ticks.Add(new Tick
                {
                    Source = MarketExchange.Kraken.ToString(),
                    Symbol = symbol,
                    Price = price.Value,
                    Volume = volume.Value,
                    Timestamp = timestamp,
                    TradeId = tradeId
                });
            }

            return ticks;
        }
        catch (JsonException)
        {
            return Array.Empty<Tick>();
        }
    }

    private static decimal? GetDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
            return number;

        if (element.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static DateTimeOffset? TryParseUnixSeconds(string secondsRaw)
    {
        if (!double.TryParse(secondsRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
            return null;

        var millis = (long)Math.Floor(seconds * 1000d);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis);
    }
}


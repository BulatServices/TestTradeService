using System.Globalization;
using System.Text.Json;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Coinbase;

/// <summary>
/// Парсеры payload для сделок Coinbase (WebSocket <c>matches</c> и REST trades).
/// </summary>
public static class CoinbaseTradesParsing
{
    /// <summary>
    /// Пытается распарсить WebSocket payload Coinbase Exchange и вернуть 0..N тиков.
    /// </summary>
    /// <param name="payload">Сырой JSON payload.</param>
    /// <param name="receivedAt">Локальная метка времени получения сообщения.</param>
    /// <returns>Список тиков (может быть пустым).</returns>
    public static IReadOnlyCollection<Tick> ParseWebSocketMatches(string payload, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<Tick>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<Tick>();

            var root = document.RootElement;
            var type = GetString(root, "type");
            if (!string.Equals(type, "match", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<Tick>();

            var productId = GetString(root, "product_id");
            var price = GetDecimal(root, "price");
            var size = GetDecimal(root, "size");
            var time = GetDateTimeOffset(root, "time") ?? receivedAt;
            var tradeId = GetString(root, "trade_id");

            if (string.IsNullOrWhiteSpace(productId) || !price.HasValue || !size.HasValue)
                return Array.Empty<Tick>();

            return new[]
            {
                new Tick
                {
                    Source = MarketExchange.Coinbase.ToString(),
                    Symbol = productId!,
                    Price = price.Value,
                    Volume = size.Value,
                    Timestamp = time,
                    TradeId = tradeId
                }
            };
        }
        catch (JsonException)
        {
            return Array.Empty<Tick>();
        }
    }

    /// <summary>
    /// Пытается распарсить REST ответ Coinbase Exchange trades и вернуть 0..N тиков.
    /// </summary>
    /// <param name="symbol">Символ (product id), например <c>BTC-USD</c>.</param>
    /// <param name="payload">Сырой JSON payload.</param>
    /// <returns>Список тиков (может быть пустым).</returns>
    public static IReadOnlyCollection<Tick> ParseRestTrades(string symbol, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<Tick>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<Tick>();

            var ticks = new List<Tick>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                var tradeId = GetString(element, "trade_id");
                var price = GetDecimal(element, "price");
                var size = GetDecimal(element, "size");
                var time = GetDateTimeOffset(element, "time") ?? DateTimeOffset.UtcNow;

                if (!price.HasValue || !size.HasValue)
                    continue;

                ticks.Add(new Tick
                {
                    Source = MarketExchange.Coinbase.ToString(),
                    Symbol = symbol,
                    Price = price.Value,
                    Volume = size.Value,
                    Timestamp = time,
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

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
            return number;

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

        return null;
    }
}


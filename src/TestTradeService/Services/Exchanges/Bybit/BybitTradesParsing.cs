using System.Globalization;
using System.Text.Json;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Bybit;

/// <summary>
/// Парсеры payload для сделок Bybit (WebSocket publicTrade и REST recent-trade).
/// </summary>
public static class BybitTradesParsing
{
    /// <summary>
    /// Пытается распарсить WebSocket payload Bybit (v5 publicTrade.*) и вернуть 0..N тиков сделок.
    /// </summary>
    /// <param name="payload">Сырой JSON payload.</param>
    /// <param name="receivedAt">Локальная метка времени получения сообщения.</param>
    /// <returns>Список тиков (может быть пустым).</returns>
    public static IReadOnlyCollection<Tick> ParseWebSocketPublicTrades(string payload, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<Tick>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<Tick>();

            var root = document.RootElement;
            var topic = GetString(root, "topic");
            if (string.IsNullOrWhiteSpace(topic) || !topic.StartsWith("publicTrade.", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<Tick>();

            var fallbackSymbol = topic["publicTrade.".Length..];

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return Array.Empty<Tick>();

            var ticks = new List<Tick>();
            foreach (var trade in data.EnumerateArray())
            {
                if (trade.ValueKind != JsonValueKind.Object)
                    continue;

                var symbol = GetString(trade, "s") ?? fallbackSymbol;
                var price = GetDecimal(trade, "p");
                var volume = GetDecimal(trade, "v");
                var tradeId = GetString(trade, "i") ?? GetString(trade, "execId");
                var timestamp = GetUnixMsAsDateTimeOffset(trade, "T") ?? receivedAt;

                if (string.IsNullOrWhiteSpace(symbol) || !price.HasValue || !volume.HasValue)
                    continue;

                ticks.Add(new Tick
                {
                    Source = MarketExchange.Bybit.ToString(),
                    Symbol = symbol!,
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

    /// <summary>
    /// Пытается распарсить REST ответ Bybit recent-trade и вернуть 0..N тиков сделок.
    /// </summary>
    /// <param name="symbol">Символ, например <c>BTCUSDT</c>.</param>
    /// <param name="payload">Сырой JSON payload.</param>
    /// <returns>Список тиков (может быть пустым).</returns>
    public static IReadOnlyCollection<Tick> ParseRestRecentTrades(string symbol, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<Tick>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<Tick>();

            if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                return Array.Empty<Tick>();

            if (!result.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                return Array.Empty<Tick>();

            var ticks = new List<Tick>();
            foreach (var trade in list.EnumerateArray())
            {
                if (trade.ValueKind != JsonValueKind.Object)
                    continue;

                var price = GetDecimal(trade, "price");
                var volume = GetDecimal(trade, "size");
                var tradeId = GetString(trade, "execId");
                var timestamp = GetUnixMsAsDateTimeOffset(trade, "time") ?? DateTimeOffset.UtcNow;

                if (!price.HasValue || !volume.HasValue)
                    continue;

                ticks.Add(new Tick
                {
                    Source = MarketExchange.Bybit.ToString(),
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

    private static DateTimeOffset? GetUnixMsAsDateTimeOffset(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixMs))
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out unixMs))
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);

        return null;
    }
}


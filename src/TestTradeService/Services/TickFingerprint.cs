using System.Globalization;

namespace TestTradeService.Services;

/// <summary>
/// Формирует строковой fingerprint тика для дедупликации.
/// </summary>
internal static class TickFingerprint
{
    /// <summary>
    /// Строит fingerprint тика для дедупликации.
    /// </summary>
    /// <param name="source">Источник/биржа.</param>
    /// <param name="symbol">Символ инструмента.</param>
    /// <param name="ts">Метка времени тика.</param>
    /// <param name="price">Цена.</param>
    /// <param name="volume">Объём.</param>
    /// <param name="tradeId">Внешний идентификатор сделки (если есть).</param>
    /// <returns>Fingerprint тика.</returns>
    internal static string Build(string source, string symbol, DateTimeOffset ts, decimal price, decimal volume, string? tradeId)
    {
        if (!string.IsNullOrWhiteSpace(tradeId))
            return $"{source}:{symbol}:id:{tradeId}";

        var tsMs = ts.ToUnixTimeMilliseconds();
        var priceStr = price.ToString(CultureInfo.InvariantCulture);
        var volumeStr = volume.ToString(CultureInfo.InvariantCulture);
        return $"{source}:{symbol}:tsms:{tsMs}:p:{priceStr}:v:{volumeStr}";
    }
}


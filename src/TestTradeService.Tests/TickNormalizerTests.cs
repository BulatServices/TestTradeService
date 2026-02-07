using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты нормализации тиков и формирования fingerprint.
/// </summary>
public sealed class TickNormalizerTests
{
    /// <summary>
    /// Проверяет, что при наличии TradeId fingerprint зависит только от Source/Symbol/TradeId,
    /// и не зависит от timestamp/price/volume.
    /// </summary>
    [Fact]
    public void Normalize_WithTradeId_FingerprintUsesTradeIdOnly()
    {
        var normalizer = new TickNormalizer();

        var tick1 = new Tick
        {
            Source = "Coinbase",
            Symbol = "BTC-USD",
            Price = 50_000.1m,
            Volume = 0.001m,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000),
            TradeId = "123"
        };

        var tick2 = tick1 with
        {
            Price = 49_000.2m,
            Volume = 0.002m,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123)
        };

        var normalized1 = normalizer.Normalize(tick1);
        var normalized2 = normalizer.Normalize(tick2);

        Assert.Equal("Coinbase:BTC-USD:id:123", normalized1.Fingerprint);
        Assert.Equal(normalized1.Fingerprint, normalized2.Fingerprint);
    }

    /// <summary>
    /// Проверяет fallback без TradeId: fingerprint использует timestamp с точностью до миллисекунд
    /// и инвариантное представление price/volume.
    /// </summary>
    [Fact]
    public void Normalize_WithoutTradeId_FingerprintUsesUnixMsPriceVolumeInvariant()
    {
        var normalizer = new TickNormalizer();

        var tick = new Tick
        {
            Source = "Bybit",
            Symbol = "BTCUSDT",
            Price = 50_000.1m,
            Volume = 0.001m,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123),
            TradeId = null
        };

        var normalized = normalizer.Normalize(tick);

        Assert.Equal("Bybit:BTCUSDT:tsms:1700000000123:p:50000.1:v:0.001", normalized.Fingerprint);
    }
}


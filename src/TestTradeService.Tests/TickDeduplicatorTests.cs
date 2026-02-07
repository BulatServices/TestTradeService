using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты дедупликации нормализованных тиков.
/// </summary>
public sealed class TickDeduplicatorTests
{
    /// <summary>
    /// Проверяет, что первый тик не считается дубликатом, а второй с тем же fingerprint считается дубликатом.
    /// </summary>
    [Fact]
    public void IsDuplicate_SameFingerprint_ReturnsTrueOnSecondCall()
    {
        var deduplicator = new TickDeduplicator();

        var tick = new NormalizedTick
        {
            Source = "Kraken",
            Symbol = "XBT/USD",
            Price = 50_000.1m,
            Volume = 0.002m,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123),
            Fingerprint = "Kraken:XBT/USD:id:abc"
        };

        Assert.False(deduplicator.IsDuplicate(tick));
        Assert.True(deduplicator.IsDuplicate(tick));
    }
}


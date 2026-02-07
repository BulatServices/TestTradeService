using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Отвечает за обнаружение дубликатов тиков в ограниченном временном окне.
/// </summary>
public sealed class TickDeduplicator
{
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, DateTimeOffset> _seenAtUtc = new(StringComparer.Ordinal);

    /// <summary>
    /// Проверяет тик на дубликат и регистрирует его отпечаток при первом появлении.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <returns><c>true</c>, если тик уже встречался.</returns>
    public bool IsDuplicate(NormalizedTick tick)
    {
        var now = DateTimeOffset.UtcNow;
        Cleanup(now);

        if (_seenAtUtc.ContainsKey(tick.Fingerprint))
        {
            return true;
        }

        _seenAtUtc[tick.Fingerprint] = now;
        return false;
    }

    private void Cleanup(DateTimeOffset now)
    {
        var expired = _seenAtUtc.Where(pair => now - pair.Value > _ttl).Select(pair => pair.Key).ToList();
        foreach (var key in expired)
        {
            _seenAtUtc.Remove(key);
        }
    }
}

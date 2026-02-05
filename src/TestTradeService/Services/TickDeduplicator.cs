using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Отвечает за обнаружение дубликатов тиков в ограниченном временном окне.
/// </summary>
public sealed class TickDeduplicator
{
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);

    /// <summary>
    /// Проверяет тик на дубликат и регистрирует его отпечаток при первом появлении.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <returns><c>true</c>, если тик уже встречался.</returns>
    public bool IsDuplicate(NormalizedTick tick)
    {
        Cleanup(tick.Timestamp);

        if (_seen.ContainsKey(tick.Fingerprint))
        {
            return true;
        }

        _seen[tick.Fingerprint] = tick.Timestamp;
        return false;
    }

    private void Cleanup(DateTimeOffset now)
    {
        var expired = _seen.Where(pair => now - pair.Value > _ttl).Select(pair => pair.Key).ToList();
        foreach (var key in expired)
        {
            _seen.Remove(key);
        }
    }
}

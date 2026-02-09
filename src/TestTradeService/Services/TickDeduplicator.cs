using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Отвечает за обнаружение дубликатов тиков в ограниченном временном окне.
/// </summary>
public sealed class TickDeduplicator
{
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, long> _seenAtEpochSeconds = new(StringComparer.Ordinal);
    private readonly Queue<(string Fingerprint, long SeenAtEpochSeconds)> _cleanupQueue = new();
    private long _nextCleanupEpochSeconds;

    /// <summary>
    /// Проверяет тик на дубликат и регистрирует его отпечаток при первом появлении.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <returns><c>true</c>, если тик уже встречался.</returns>
    public bool IsDuplicate(NormalizedTick tick)
    {
        var nowEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowEpochSeconds >= _nextCleanupEpochSeconds)
        {
            Cleanup(nowEpochSeconds);
        }

        if (_seenAtEpochSeconds.TryGetValue(tick.Fingerprint, out var seenAtEpochSeconds))
        {
            if (nowEpochSeconds - seenAtEpochSeconds <= (long)_ttl.TotalSeconds)
            {
                return true;
            }
        }

        _seenAtEpochSeconds[tick.Fingerprint] = nowEpochSeconds;
        _cleanupQueue.Enqueue((tick.Fingerprint, nowEpochSeconds));
        return false;
    }

    private void Cleanup(long nowEpochSeconds)
    {
        var ttlSeconds = (long)_ttl.TotalSeconds;
        while (_cleanupQueue.Count > 0)
        {
            var entry = _cleanupQueue.Peek();
            if (nowEpochSeconds - entry.SeenAtEpochSeconds <= ttlSeconds)
            {
                break;
            }

            _cleanupQueue.Dequeue();
            if (_seenAtEpochSeconds.TryGetValue(entry.Fingerprint, out var currentSeenAt)
                && currentSeenAt == entry.SeenAtEpochSeconds)
            {
                _seenAtEpochSeconds.Remove(entry.Fingerprint);
            }
        }

        _nextCleanupEpochSeconds = nowEpochSeconds + 5;
    }
}

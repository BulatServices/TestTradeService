using System.Collections.Concurrent;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Monitoring;

/// <summary>
/// Сервис мониторинга производительности обработки по источникам данных.
/// </summary>
public sealed class MonitoringService : IMonitoringService
{
    private readonly ConcurrentDictionary<string, SourceMetrics> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<string> _warnings = new();

    /// <summary>
    /// Регистрирует обработанный тик.
    /// </summary>
    public void RecordTick(string sourceName, NormalizedTick tick)
    {
        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName));
        metrics.RecordTick(tick);
    }

    /// <summary>
    /// Регистрирует сформированный агрегат.
    /// </summary>
    public void RecordAggregate(string sourceName, AggregatedCandle candle)
    {
        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName));
        metrics.RecordAggregate();
    }

    /// <summary>
    /// Регистрирует задержку обработки.
    /// </summary>
    public void RecordDelay(string sourceName, TimeSpan delay)
    {
        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName));
        metrics.RecordDelay(delay);

        if (delay > TimeSpan.FromSeconds(2))
        {
            _warnings.Add($"Delay warning for {sourceName}: {delay.TotalMilliseconds:F0} ms");
        }
    }

    /// <summary>
    /// Возвращает снимок текущих метрик и предупреждений.
    /// </summary>
    public MonitoringSnapshot Snapshot()
    {
        var snapshot = _metrics.Values
            .Select(m => m.ToStats())
            .ToDictionary(stat => stat.Source, stat => stat);

        return new MonitoringSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            SourceStats = snapshot,
            Warnings = _warnings.ToList()
        };
    }

    private sealed class SourceMetrics
    {
        private readonly string _source;
        private long _tickCount;
        private long _aggregateCount;
        private readonly object _delayLock = new();
        private double _delaySum;
        private long _delaySamples;
        private DateTimeOffset _lastTickTime;

        /// <summary>
        /// Создает контейнер метрик для конкретного источника.
        /// </summary>
        public SourceMetrics(string source)
        {
            _source = source;
        }

        /// <summary>
        /// Учитывает поступивший тик.
        /// </summary>
        public void RecordTick(NormalizedTick tick)
        {
            Interlocked.Increment(ref _tickCount);
            _lastTickTime = tick.Timestamp;
        }

        /// <summary>
        /// Учитывает сформированный агрегат.
        /// </summary>
        public void RecordAggregate()
        {
            Interlocked.Increment(ref _aggregateCount);
        }

        /// <summary>
        /// Учитывает значение задержки обработки.
        /// </summary>
        public void RecordDelay(TimeSpan delay)
        {
            lock (_delayLock)
            {
                _delaySamples++;
                _delaySum += delay.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Формирует итоговую статистику по источнику.
        /// </summary>
        public SourceStats ToStats()
        {
            double averageDelay;
            lock (_delayLock)
            {
                averageDelay = _delaySamples == 0 ? 0 : _delaySum / _delaySamples;
            }

            return new SourceStats
            {
                Source = _source,
                TickCount = _tickCount,
                AggregateCount = _aggregateCount,
                AverageDelayMs = averageDelay,
                LastTickTime = _lastTickTime
            };
        }
    }
}

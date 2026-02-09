using System.Collections.Concurrent;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Monitoring.Configuration;

namespace TestTradeService.Monitoring;

/// <summary>
/// Сервис мониторинга производительности обработки по источникам данных.
/// </summary>
public sealed class MonitoringService : IMonitoringService
{
    private readonly ConcurrentDictionary<string, SourceMetrics> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<MarketExchange, ExchangeMetrics> _exchangeMetrics = new();
    private readonly int _windowSeconds;
    private readonly double _warnDelayMs;
    private readonly double _criticalDelayMs;

    /// <summary>
    /// Инициализирует сервис мониторинга с настройками SLA.
    /// </summary>
    /// <param name="slaConfig">Конфигурация SLA.</param>
    public MonitoringService(MonitoringSlaConfig slaConfig)
    {
        _windowSeconds = Math.Max(1, (int)Math.Round(slaConfig.RollingWindow.TotalSeconds, MidpointRounding.AwayFromZero));

        var baseDelayMs = Math.Max(1d, slaConfig.MaxTickDelay.TotalMilliseconds);
        _warnDelayMs = baseDelayMs * Math.Max(1d, slaConfig.WarnMultiplier);
        _criticalDelayMs = baseDelayMs * Math.Max(slaConfig.CriticalMultiplier, slaConfig.WarnMultiplier);
    }

    /// <summary>
    /// Регистрирует обработанный тик.
    /// </summary>
    /// <param name="sourceName">Имя источника данных.</param>
    /// <param name="tick">Нормализованный тик.</param>
    public void RecordTick(string sourceName, NormalizedTick tick)
    {
        var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName, _windowSeconds));
        metrics.RecordTick(tick, nowEpoch);

        var exchange = GetExchange(sourceName);
        var exchangeMetrics = _exchangeMetrics.GetOrAdd(exchange, e => new ExchangeMetrics(e, _windowSeconds));
        exchangeMetrics.RecordTick(tick, nowEpoch);
    }

    /// <summary>
    /// Регистрирует сформированный агрегат.
    /// </summary>
    /// <param name="sourceName">Имя источника данных.</param>
    /// <param name="candle">Агрегированная свеча.</param>
    public void RecordAggregate(string sourceName, AggregatedCandle candle)
    {
        var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName, _windowSeconds));
        metrics.RecordAggregate(nowEpoch);

        var exchange = GetExchange(sourceName);
        var exchangeMetrics = _exchangeMetrics.GetOrAdd(exchange, e => new ExchangeMetrics(e, _windowSeconds));
        exchangeMetrics.RecordAggregate(nowEpoch);
    }

    /// <summary>
    /// Регистрирует задержку обработки.
    /// </summary>
    /// <param name="sourceName">Имя источника данных.</param>
    /// <param name="delay">Задержка обработки.</param>
    public void RecordDelay(string sourceName, TimeSpan delay)
    {
        var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName, _windowSeconds));
        metrics.RecordDelay(delay, nowEpoch);

        var exchange = GetExchange(sourceName);
        var exchangeMetrics = _exchangeMetrics.GetOrAdd(exchange, e => new ExchangeMetrics(e, _windowSeconds));
        exchangeMetrics.RecordDelay(delay, nowEpoch);
    }

    /// <summary>
    /// Возвращает снимок текущих метрик и предупреждений.
    /// </summary>
    /// <returns>Снимок мониторинга.</returns>
    public MonitoringSnapshot Snapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var nowEpoch = now.ToUnixTimeSeconds();

        var sourceSnapshots = _metrics.Values
            .Select(metric => metric.ToSnapshot(now, nowEpoch, _warnDelayMs, _criticalDelayMs))
            .ToList();

        var sourceStats = sourceSnapshots
            .Select(item => item.Stats)
            .ToDictionary(stat => stat.Source, stat => stat, StringComparer.OrdinalIgnoreCase);

        var exchangeStats = _exchangeMetrics.Values
            .Select(metric => metric.ToStats(nowEpoch))
            .ToDictionary(stat => stat.Exchange, stat => stat);

        var warnings = sourceSnapshots
            .Where(item => item.Stats.Status != MonitoringSourceStatus.Ok)
            .OrderByDescending(item => item.Stats.Status)
            .ThenBy(item => item.Stats.Source, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
                $"{item.Stats.Status}: {item.Stats.Source} age={item.Stats.LastTickAgeMs:F0}ms avgDelay={item.Stats.WindowAvgDelayMs:F0}ms maxDelay={item.Stats.WindowMaxDelayMs:F0}ms")
            .ToList();

        var totalWindowTickCount = sourceSnapshots.Sum(item => item.Window.TickCount);
        var totalWindowAggregateCount = sourceSnapshots.Sum(item => item.Window.AggregateCount);
        var totalWindowDelaySamples = sourceSnapshots.Sum(item => item.Window.DelaySamples);
        var totalWindowDelaySum = sourceSnapshots.Sum(item => item.Window.DelaySumMs);
        var totalWindowMaxDelayMs = sourceSnapshots.Count == 0 ? 0 : sourceSnapshots.Max(item => item.Window.MaxDelayMs);

        var performanceReport = new PerformanceReport
        {
            WindowMinutes = Math.Max(1, (int)Math.Round(_windowSeconds / 60d, MidpointRounding.AwayFromZero)),
            TotalWindowTickCount = totalWindowTickCount,
            TotalWindowAggregateCount = totalWindowAggregateCount,
            TotalWindowAvgDelayMs = totalWindowDelaySamples == 0 ? 0 : totalWindowDelaySum / totalWindowDelaySamples,
            TotalWindowMaxDelayMs = totalWindowMaxDelayMs,
            TotalWindowTickRatePerSec = totalWindowTickCount / (double)_windowSeconds,
            TotalWindowAggregateRatePerSec = totalWindowAggregateCount / (double)_windowSeconds,
            SourcesOk = sourceSnapshots.Count(item => item.Stats.Status == MonitoringSourceStatus.Ok),
            SourcesWarn = sourceSnapshots.Count(item => item.Stats.Status == MonitoringSourceStatus.Warn),
            SourcesCritical = sourceSnapshots.Count(item => item.Stats.Status == MonitoringSourceStatus.Critical)
        };

        return new MonitoringSnapshot
        {
            Timestamp = now,
            ExchangeStats = exchangeStats,
            SourceStats = sourceStats,
            PerformanceReport = performanceReport,
            Warnings = warnings
        };
    }

    private static MarketExchange GetExchange(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return MarketExchange.Unknown;

        var delimiterIndex = sourceName.IndexOf('-', StringComparison.Ordinal);
        var head = delimiterIndex > 0 ? sourceName[..delimiterIndex] : sourceName;
        return Enum.TryParse<MarketExchange>(head, true, out var exchange) ? exchange : MarketExchange.Unknown;
    }

    private readonly record struct SourceSnapshotResult(SourceStats Stats, WindowSnapshot Window);

    private sealed class SourceMetrics
    {
        private readonly string _source;
        private readonly RollingWindowCounter _windowCounter;
        private long _tickCount;
        private long _aggregateCount;
        private readonly object _delayLock = new();
        private double _delaySum;
        private long _delaySamples;
        private readonly object _lastTickLock = new();
        private DateTimeOffset _lastTickTime;

        public SourceMetrics(string source, int windowSeconds)
        {
            _source = source;
            _windowCounter = new RollingWindowCounter(windowSeconds);
        }

        public void RecordTick(NormalizedTick tick, long nowEpoch)
        {
            Interlocked.Increment(ref _tickCount);
            lock (_lastTickLock)
            {
                _lastTickTime = tick.Timestamp;
            }

            _windowCounter.RecordTick(nowEpoch);
        }

        public void RecordAggregate(long nowEpoch)
        {
            Interlocked.Increment(ref _aggregateCount);
            _windowCounter.RecordAggregate(nowEpoch);
        }

        public void RecordDelay(TimeSpan delay, long nowEpoch)
        {
            lock (_delayLock)
            {
                _delaySamples++;
                _delaySum += delay.TotalMilliseconds;
            }

            _windowCounter.RecordDelay(nowEpoch, delay.TotalMilliseconds);
        }

        public SourceSnapshotResult ToSnapshot(DateTimeOffset now, long nowEpoch, double warnDelayMs, double criticalDelayMs)
        {
            var window = _windowCounter.Snapshot(nowEpoch);

            double averageDelay;
            lock (_delayLock)
            {
                averageDelay = _delaySamples == 0 ? 0 : _delaySum / _delaySamples;
            }

            DateTimeOffset lastTick;
            lock (_lastTickLock)
            {
                lastTick = _lastTickTime;
            }

            var lastTickAgeMs = lastTick == default
                ? 0
                : Math.Max(0, (now - lastTick).TotalMilliseconds);

            var stats = new SourceStats
            {
                Source = _source,
                TickCount = _tickCount,
                AggregateCount = _aggregateCount,
                AverageDelayMs = averageDelay,
                LastTickTime = lastTick,
                LastTickAgeMs = lastTickAgeMs,
                WindowTickCount = window.TickCount,
                WindowAggregateCount = window.AggregateCount,
                WindowAvgDelayMs = window.DelaySamples == 0 ? 0 : window.DelaySumMs / window.DelaySamples,
                WindowMaxDelayMs = window.MaxDelayMs,
                WindowTickRatePerSec = window.TickCount / (double)window.WindowSeconds,
                WindowAggregateRatePerSec = window.AggregateCount / (double)window.WindowSeconds,
                Status = MonitoringSourceStatus.Ok
            };

            var status = ResolveStatus(stats, warnDelayMs, criticalDelayMs);
            stats = stats with { Status = status };
            return new SourceSnapshotResult(stats, window);
        }

        private static MonitoringSourceStatus ResolveStatus(SourceStats stats, double warnDelayMs, double criticalDelayMs)
        {
            if (stats.LastTickAgeMs > criticalDelayMs || stats.WindowAvgDelayMs > criticalDelayMs || stats.WindowMaxDelayMs > criticalDelayMs)
            {
                return MonitoringSourceStatus.Critical;
            }

            if (stats.LastTickAgeMs > warnDelayMs || stats.WindowAvgDelayMs > warnDelayMs || stats.WindowMaxDelayMs > warnDelayMs)
            {
                return MonitoringSourceStatus.Warn;
            }

            return MonitoringSourceStatus.Ok;
        }
    }

    private sealed class ExchangeMetrics
    {
        private readonly MarketExchange _exchange;
        private readonly RollingWindowCounter _windowCounter;
        private long _tickCount;
        private long _aggregateCount;
        private readonly object _delayLock = new();
        private double _delaySum;
        private long _delaySamples;
        private readonly object _lastTickLock = new();
        private DateTimeOffset _lastTickTime;

        public ExchangeMetrics(MarketExchange exchange, int windowSeconds)
        {
            _exchange = exchange;
            _windowCounter = new RollingWindowCounter(windowSeconds);
        }

        public void RecordTick(NormalizedTick tick, long nowEpoch)
        {
            Interlocked.Increment(ref _tickCount);
            lock (_lastTickLock)
            {
                _lastTickTime = tick.Timestamp;
            }

            _windowCounter.RecordTick(nowEpoch);
        }

        public void RecordAggregate(long nowEpoch)
        {
            Interlocked.Increment(ref _aggregateCount);
            _windowCounter.RecordAggregate(nowEpoch);
        }

        public void RecordDelay(TimeSpan delay, long nowEpoch)
        {
            lock (_delayLock)
            {
                _delaySamples++;
                _delaySum += delay.TotalMilliseconds;
            }

            _windowCounter.RecordDelay(nowEpoch, delay.TotalMilliseconds);
        }

        public ExchangeStats ToStats(long nowEpoch)
        {
            var window = _windowCounter.Snapshot(nowEpoch);

            double averageDelay;
            lock (_delayLock)
            {
                averageDelay = _delaySamples == 0 ? 0 : _delaySum / _delaySamples;
            }

            DateTimeOffset lastTick;
            lock (_lastTickLock)
            {
                lastTick = _lastTickTime;
            }

            return new ExchangeStats
            {
                Exchange = _exchange,
                TickCount = _tickCount,
                AggregateCount = _aggregateCount,
                AverageDelayMs = averageDelay,
                LastTickTime = lastTick,
                WindowTickCount = window.TickCount,
                WindowAggregateCount = window.AggregateCount,
                WindowAvgDelayMs = window.DelaySamples == 0 ? 0 : window.DelaySumMs / window.DelaySamples,
                WindowMaxDelayMs = window.MaxDelayMs,
                WindowTickRatePerSec = window.TickCount / (double)window.WindowSeconds,
                WindowAggregateRatePerSec = window.AggregateCount / (double)window.WindowSeconds
            };
        }
    }

    private readonly record struct WindowSnapshot(
        int WindowSeconds,
        long TickCount,
        long AggregateCount,
        long DelaySamples,
        double DelaySumMs,
        double MaxDelayMs);

    private sealed class RollingWindowCounter
    {
        private readonly WindowBucket[] _buckets;
        private readonly int _windowSeconds;
        private readonly object _sync = new();

        public RollingWindowCounter(int windowSeconds)
        {
            _windowSeconds = Math.Max(1, windowSeconds);
            _buckets = Enumerable.Range(0, _windowSeconds).Select(_ => new WindowBucket()).ToArray();
        }

        public void RecordTick(long epochSecond)
        {
            lock (_sync)
            {
                var bucket = GetBucket(epochSecond);
                bucket.TickCount++;
            }
        }

        public void RecordAggregate(long epochSecond)
        {
            lock (_sync)
            {
                var bucket = GetBucket(epochSecond);
                bucket.AggregateCount++;
            }
        }

        public void RecordDelay(long epochSecond, double delayMs)
        {
            lock (_sync)
            {
                var bucket = GetBucket(epochSecond);
                bucket.DelaySamples++;
                bucket.DelaySumMs += delayMs;
                if (delayMs > bucket.MaxDelayMs)
                {
                    bucket.MaxDelayMs = delayMs;
                }
            }
        }

        public WindowSnapshot Snapshot(long nowEpochSecond)
        {
            lock (_sync)
            {
                long tickCount = 0;
                long aggregateCount = 0;
                long delaySamples = 0;
                double delaySumMs = 0;
                double maxDelayMs = 0;

                foreach (var bucket in _buckets)
                {
                    var age = nowEpochSecond - bucket.Second;
                    if (bucket.Second <= 0 || age < 0 || age >= _windowSeconds)
                    {
                        continue;
                    }

                    tickCount += bucket.TickCount;
                    aggregateCount += bucket.AggregateCount;
                    delaySamples += bucket.DelaySamples;
                    delaySumMs += bucket.DelaySumMs;
                    if (bucket.MaxDelayMs > maxDelayMs)
                    {
                        maxDelayMs = bucket.MaxDelayMs;
                    }
                }

                return new WindowSnapshot(_windowSeconds, tickCount, aggregateCount, delaySamples, delaySumMs, maxDelayMs);
            }
        }

        private WindowBucket GetBucket(long epochSecond)
        {
            var index = (int)(Math.Abs(epochSecond % _windowSeconds));
            var bucket = _buckets[index];
            if (bucket.Second != epochSecond)
            {
                bucket.Reset(epochSecond);
            }

            return bucket;
        }

        private sealed class WindowBucket
        {
            public long Second { get; private set; }

            public long TickCount { get; set; }

            public long AggregateCount { get; set; }

            public long DelaySamples { get; set; }

            public double DelaySumMs { get; set; }

            public double MaxDelayMs { get; set; }

            public void Reset(long second)
            {
                Second = second;
                TickCount = 0;
                AggregateCount = 0;
                DelaySamples = 0;
                DelaySumMs = 0;
                MaxDelayMs = 0;
            }
        }
    }
}

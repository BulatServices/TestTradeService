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
    private readonly ConcurrentBag<string> _warnings = new();
    private readonly MonitoringSlaConfig _slaConfig;

    /// <summary>
    /// Инициализирует сервис мониторинга с настройками SLA.
    /// </summary>
    /// <param name="slaConfig">Конфигурация SLA.</param>
    public MonitoringService(MonitoringSlaConfig slaConfig)
    {
        _slaConfig = slaConfig;
    }

    /// <summary>
    /// Регистрирует обработанный тик.
    /// </summary>
    /// <param name="sourceName">Имя источника данных.</param>
    /// <param name="tick">Нормализованный тик.</param>
    public void RecordTick(string sourceName, NormalizedTick tick)
    {
        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName));
        metrics.RecordTick(tick);

        var exchange = GetExchange(sourceName);
        var exchangeMetrics = _exchangeMetrics.GetOrAdd(exchange, e => new ExchangeMetrics(e));
        exchangeMetrics.RecordTick(tick);
    }

    /// <summary>
    /// Регистрирует сформированный агрегат.
    /// </summary>
    /// <param name="sourceName">Имя источника данных.</param>
    /// <param name="candle">Агрегированная свеча.</param>
    public void RecordAggregate(string sourceName, AggregatedCandle candle)
    {
        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName));
        metrics.RecordAggregate();

        var exchange = GetExchange(sourceName);
        var exchangeMetrics = _exchangeMetrics.GetOrAdd(exchange, e => new ExchangeMetrics(e));
        exchangeMetrics.RecordAggregate();
    }

    /// <summary>
    /// Регистрирует задержку обработки.
    /// </summary>
    /// <param name="sourceName">Имя источника данных.</param>
    /// <param name="delay">Задержка обработки.</param>
    public void RecordDelay(string sourceName, TimeSpan delay)
    {
        var metrics = _metrics.GetOrAdd(sourceName, _ => new SourceMetrics(sourceName));
        metrics.RecordDelay(delay);

        var exchange = GetExchange(sourceName);
        var exchangeMetrics = _exchangeMetrics.GetOrAdd(exchange, e => new ExchangeMetrics(e));
        exchangeMetrics.RecordDelay(delay);

        if (delay > _slaConfig.MaxTickDelay)
        {
            _warnings.Add($"Delay warning for {sourceName} ({exchange}): {delay.TotalMilliseconds:F0} ms");
        }
    }

    /// <summary>
    /// Возвращает снимок текущих метрик и предупреждений.
    /// </summary>
    /// <returns>Снимок мониторинга.</returns>
    public MonitoringSnapshot Snapshot()
    {
        var exchangeSnapshot = _exchangeMetrics.Values
            .Select(m => m.ToStats())
            .ToDictionary(stat => stat.Exchange, stat => stat);

        var snapshot = _metrics.Values
            .Select(m => m.ToStats())
            .ToDictionary(stat => stat.Source, stat => stat);

        return new MonitoringSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ExchangeStats = exchangeSnapshot,
            SourceStats = snapshot,
            Warnings = _warnings.ToList()
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
        /// <param name="source">Имя источника.</param>
        public SourceMetrics(string source)
        {
            _source = source;
        }

        /// <summary>
        /// Учитывает поступивший тик.
        /// </summary>
        /// <param name="tick">Нормализованный тик.</param>
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
        /// <param name="delay">Задержка обработки.</param>
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
        /// <returns>Статистика по источнику.</returns>
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

    private sealed class ExchangeMetrics
    {
        private readonly MarketExchange _exchange;
        private long _tickCount;
        private long _aggregateCount;
        private readonly object _delayLock = new();
        private double _delaySum;
        private long _delaySamples;
        private DateTimeOffset _lastTickTime;

        public ExchangeMetrics(MarketExchange exchange)
        {
            _exchange = exchange;
        }

        public void RecordTick(NormalizedTick tick)
        {
            Interlocked.Increment(ref _tickCount);
            _lastTickTime = tick.Timestamp;
        }

        public void RecordAggregate()
        {
            Interlocked.Increment(ref _aggregateCount);
        }

        public void RecordDelay(TimeSpan delay)
        {
            lock (_delayLock)
            {
                _delaySamples++;
                _delaySum += delay.TotalMilliseconds;
            }
        }

        public ExchangeStats ToStats()
        {
            double averageDelay;
            lock (_delayLock)
            {
                averageDelay = _delaySamples == 0 ? 0 : _delaySum / _delaySamples;
            }

            return new ExchangeStats
            {
                Exchange = _exchange,
                TickCount = _tickCount,
                AggregateCount = _aggregateCount,
                AverageDelayMs = averageDelay,
                LastTickTime = _lastTickTime
            };
        }
    }
}

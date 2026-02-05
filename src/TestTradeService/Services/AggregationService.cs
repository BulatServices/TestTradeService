using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Рассчитывает свечи по временным окнам и производные метрики по инструментам.
/// </summary>
public sealed class AggregationService : IAggregationService
{
    private readonly Dictionary<string, Dictionary<TimeSpan, CandleBuilder>> _builders = new();
    private readonly Dictionary<string, MetricsBuilder> _metrics = new();
    private static readonly TimeSpan[] Windows =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromHours(1)
    };

    /// <summary>
    /// Обновляет свечные агрегаты по входящему тику.
    /// </summary>
    public IEnumerable<AggregatedCandle> Update(NormalizedTick tick)
    {
        var output = new List<AggregatedCandle>();
        foreach (var window in Windows)
        {
            var builder = GetBuilder(tick.Symbol, window);
            var candle = builder.Add(tick);
            if (candle is not null)
            {
                output.Add(candle);
            }
        }

        return output;
    }

    /// <summary>
    /// Обновляет расчет rolling-метрик по символу.
    /// </summary>
    public MetricsSnapshot UpdateMetrics(NormalizedTick tick)
    {
        var key = tick.Symbol;
        if (!_metrics.TryGetValue(key, out var builder))
        {
            builder = new MetricsBuilder(TimeSpan.FromMinutes(1));
            _metrics[key] = builder;
        }

        return builder.Add(tick);
    }

    private CandleBuilder GetBuilder(string symbol, TimeSpan window)
    {
        if (!_builders.TryGetValue(symbol, out var perSymbol))
        {
            perSymbol = new Dictionary<TimeSpan, CandleBuilder>();
            _builders[symbol] = perSymbol;
        }

        if (!perSymbol.TryGetValue(window, out var builder))
        {
            builder = new CandleBuilder(window);
            perSymbol[window] = builder;
        }

        return builder;
    }

    private sealed class CandleBuilder
    {
        private readonly TimeSpan _window;
        private DateTimeOffset _windowStart = DateTimeOffset.MinValue;
        private decimal _open;
        private decimal _high;
        private decimal _low;
        private decimal _close;
        private decimal _volume;
        private int _count;

        /// <summary>
        /// Создает билдер свечей для заданного окна.
        /// </summary>
        public CandleBuilder(TimeSpan window)
        {
            _window = window;
        }

        /// <summary>
        /// Добавляет тик в окно и при необходимости завершает свечу.
        /// </summary>
        public AggregatedCandle? Add(NormalizedTick tick)
        {
            var aligned = Align(tick.Timestamp);
            if (_windowStart == DateTimeOffset.MinValue)
            {
                StartNew(aligned, tick);
                return null;
            }

            if (aligned > _windowStart)
            {
                var candle = Build(tick.Source, tick.Symbol);
                StartNew(aligned, tick);
                return candle;
            }

            Update(tick);
            return null;
        }

        private void StartNew(DateTimeOffset windowStart, NormalizedTick tick)
        {
            _windowStart = windowStart;
            _open = tick.Price;
            _high = tick.Price;
            _low = tick.Price;
            _close = tick.Price;
            _volume = tick.Volume;
            _count = 1;
        }

        private void Update(NormalizedTick tick)
        {
            _high = Math.Max(_high, tick.Price);
            _low = Math.Min(_low, tick.Price);
            _close = tick.Price;
            _volume += tick.Volume;
            _count++;
        }

        private AggregatedCandle Build(string source, string symbol)
        {
            return new AggregatedCandle
            {
                Source = source,
                Symbol = symbol,
                WindowStart = _windowStart,
                Window = _window,
                Open = _open,
                High = _high,
                Low = _low,
                Close = _close,
                Volume = _volume,
                Count = _count
            };
        }

        private DateTimeOffset Align(DateTimeOffset timestamp)
        {
            var ticks = timestamp.UtcTicks - (timestamp.UtcTicks % _window.Ticks);
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    private sealed class MetricsBuilder
    {
        private readonly TimeSpan _window;
        private readonly Queue<NormalizedTick> _ticks = new();

        /// <summary>
        /// Создает билдер метрик для rolling-окна.
        /// </summary>
        public MetricsBuilder(TimeSpan window)
        {
            _window = window;
        }

        /// <summary>
        /// Добавляет тик в окно метрик и возвращает обновленный снимок.
        /// </summary>
        public MetricsSnapshot Add(NormalizedTick tick)
        {
            var cutoff = tick.Timestamp - _window;
            _ticks.Enqueue(tick);
            while (_ticks.Count > 0 && _ticks.Peek().Timestamp < cutoff)
            {
                _ticks.Dequeue();
            }

            var count = _ticks.Count;
            var avg = count == 0 ? 0 : _ticks.Average(t => t.Price);
            var variance = count == 0 ? 0 : _ticks.Average(t => Math.Pow((double)(t.Price - avg), 2));
            var volatility = (decimal)Math.Sqrt(variance);

            return new MetricsSnapshot
            {
                Symbol = tick.Symbol,
                WindowStart = tick.Timestamp - _window,
                Window = _window,
                AveragePrice = avg,
                Volatility = volatility,
                Count = count
            };
        }
    }
}

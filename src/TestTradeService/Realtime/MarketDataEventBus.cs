using System.Threading.Channels;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Realtime;

/// <summary>
/// Bounded-очередь событий рыночного потока для трансляции через SignalR.
/// </summary>
public sealed class MarketDataEventBus : IMarketDataEventBus
{
    private readonly Channel<MarketDataEvent> _channel = Channel.CreateBounded<MarketDataEvent>(new BoundedChannelOptions(4096)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    /// <summary>
    /// Публикует событие тика.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    public void PublishTick(NormalizedTick tick)
    {
        _channel.Writer.TryWrite(new MarketDataEvent("tick", tick));
    }

    /// <summary>
    /// Публикует событие агрегированной свечи.
    /// </summary>
    /// <param name="candle">Агрегированная свеча.</param>
    public void PublishAggregate(AggregatedCandle candle)
    {
        _channel.Writer.TryWrite(new MarketDataEvent("aggregate", candle));
    }

    /// <summary>
    /// Публикует событие алерта.
    /// </summary>
    /// <param name="alert">Алерт.</param>
    public void PublishAlert(Alert alert)
    {
        _channel.Writer.TryWrite(new MarketDataEvent("alert", alert));
    }

    /// <summary>
    /// Публикует событие мониторинга.
    /// </summary>
    /// <param name="snapshot">Снимок мониторинга.</param>
    public void PublishMonitoring(MonitoringSnapshot snapshot)
    {
        _channel.Writer.TryWrite(new MarketDataEvent("monitoring", snapshot));
    }

    /// <summary>
    /// Возвращает поток событий для чтения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Поток событий шины.</returns>
    public IAsyncEnumerable<MarketDataEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

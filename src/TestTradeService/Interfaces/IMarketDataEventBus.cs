using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Шина событий торгового потока для трансляции в SignalR.
/// </summary>
public interface IMarketDataEventBus
{
    /// <summary>
    /// Публикует событие тика.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    void PublishTick(NormalizedTick tick);

    /// <summary>
    /// Публикует событие агрегированной свечи.
    /// </summary>
    /// <param name="candle">Агрегированная свеча.</param>
    void PublishAggregate(AggregatedCandle candle);

    /// <summary>
    /// Публикует событие алерта.
    /// </summary>
    /// <param name="alert">Событие алерта.</param>
    void PublishAlert(Alert alert);

    /// <summary>
    /// Публикует снимок мониторинга.
    /// </summary>
    /// <param name="snapshot">Снимок мониторинга.</param>
    void PublishMonitoring(MonitoringSnapshot snapshot);

    /// <summary>
    /// Возвращает поток событий для чтения транслятором.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Асинхронная последовательность событий.</returns>
    IAsyncEnumerable<MarketDataEvent> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Универсальное событие рыночного потока.
/// </summary>
/// <param name="Kind">Тип события.</param>
/// <param name="Payload">Полезная нагрузка.</param>
public sealed record MarketDataEvent(string Kind, object Payload);

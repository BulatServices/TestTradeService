using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Абстракция слоя хранения данных торговой системы.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Сохраняет raw-тик в хранилище.
    /// </summary>
    Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет агрегированную свечу.
    /// </summary>
    Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет метаданные инструмента.
    /// </summary>
    Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет статус источника данных.
    /// </summary>
    Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет алерт.
    /// </summary>
    Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken);
}

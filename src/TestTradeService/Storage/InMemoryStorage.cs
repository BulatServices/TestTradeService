using System.Collections.Concurrent;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage;

/// <summary>
/// Потокобезопасная in-memory реализация хранилища.
/// </summary>
public sealed class InMemoryStorage : IStorage
{
    private readonly ConcurrentBag<NormalizedTick> _ticks = new();
    private readonly ConcurrentBag<AggregatedCandle> _aggregates = new();
    private readonly ConcurrentBag<InstrumentMetadata> _instruments = new();
    private readonly ConcurrentBag<SourceStatus> _statuses = new();
    private readonly ConcurrentBag<Alert> _alerts = new();

    /// <summary>
    /// Сохраняет raw-тик.
    /// </summary>
    public Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        _ticks.Add(tick);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет агрегированную свечу.
    /// </summary>
    public Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
    {
        _aggregates.Add(candle);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет метаданные инструмента.
    /// </summary>
    public Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken)
    {
        _instruments.Add(metadata);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет статус источника.
    /// </summary>
    public Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken)
    {
        _statuses.Add(status);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет алерт.
    /// </summary>
    public Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken)
    {
        _alerts.Add(alert);
        return Task.CompletedTask;
    }
}

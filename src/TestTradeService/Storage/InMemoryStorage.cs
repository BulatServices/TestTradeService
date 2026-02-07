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
    /// <exception cref="ArgumentException">Метаданные инструмента невалидны.</exception>
    public Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken)
    {
        ValidateInstrumentMetadata(metadata);
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

    private static void ValidateInstrumentMetadata(InstrumentMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Exchange))
        {
            throw new ArgumentException("Поле Exchange должно быть задано.", nameof(metadata));
        }

        if (string.IsNullOrWhiteSpace(metadata.Symbol))
        {
            throw new ArgumentException("Поле Symbol должно быть задано.", nameof(metadata));
        }

        if (string.IsNullOrWhiteSpace(metadata.BaseAsset))
        {
            throw new ArgumentException("Поле BaseAsset должно быть задано.", nameof(metadata));
        }

        if (string.IsNullOrWhiteSpace(metadata.QuoteAsset))
        {
            throw new ArgumentException("Поле QuoteAsset должно быть задано.", nameof(metadata));
        }

        if (metadata.PriceTickSize <= 0)
        {
            throw new ArgumentException("Поле PriceTickSize должно быть больше нуля.", nameof(metadata));
        }

        if (metadata.VolumeStep <= 0)
        {
            throw new ArgumentException("Поле VolumeStep должно быть больше нуля.", nameof(metadata));
        }

        if (metadata.PriceDecimals < 0)
        {
            throw new ArgumentException("Поле PriceDecimals не может быть отрицательным.", nameof(metadata));
        }

        if (metadata.VolumeDecimals < 0)
        {
            throw new ArgumentException("Поле VolumeDecimals не может быть отрицательным.", nameof(metadata));
        }

        if (metadata.MarketType == MarketType.Perp)
        {
            if (metadata.ContractSize is null || metadata.ContractSize <= 0)
            {
                throw new ArgumentException("Для рынка perp поле ContractSize должно быть задано и больше нуля.", nameof(metadata));
            }
        }
        else if (metadata.ContractSize is not null)
        {
            throw new ArgumentException("Для рынка spot поле ContractSize должно быть null.", nameof(metadata));
        }

        if (metadata.MinNotional is not null && metadata.MinNotional <= 0)
        {
            throw new ArgumentException("Поле MinNotional должно быть больше нуля (или null).", nameof(metadata));
        }
    }
}

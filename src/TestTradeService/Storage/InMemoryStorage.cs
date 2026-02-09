using System.Collections.Concurrent;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage;

/// <summary>
/// Потокобезопасная in-memory реализация хранилища.
/// </summary>
public sealed class InMemoryStorage : IStorage
{
    private readonly ConcurrentBag<RawTick> _rawTicks = new();
    private readonly ConcurrentBag<NormalizedTick> _ticks = new();
    private readonly ConcurrentBag<AggregatedCandle> _aggregates = new();
    private readonly ConcurrentBag<InstrumentMetadata> _instruments = new();
    private readonly ConcurrentBag<SourceStatus> _statuses = new();
    private readonly ConcurrentBag<SourceStatus> _statusEvents = new();
    private readonly ConcurrentBag<Alert> _alerts = new();
    private readonly IReadOnlyCollection<AlertRuleConfig> _alertRules = DefaultConfigurationFactory.CreateAlertRules();

    /// <summary>
    /// Сохраняет сырой тик.
    /// </summary>
    /// <param name="rawTick">Сырой тик до нормализации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения сырого тика.</returns>
    public Task StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken)
    {
        _rawTicks.Add(rawTick);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет raw-тик.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения тика.</returns>
    public Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        _ticks.Add(tick);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет агрегированную свечу.
    /// </summary>
    /// <param name="candle">Агрегированная свеча.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения свечи.</returns>
    public Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
    {
        _aggregates.Add(candle);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет метаданные инструмента.
    /// </summary>
    /// <param name="metadata">Метаданные инструмента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения метаданных.</returns>
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
    /// <param name="status">Статус источника.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения статуса.</returns>
    public Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken)
    {
        _statuses.Add(status);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет событие изменения статуса источника.
    /// </summary>
    /// <param name="status">Статус источника на момент изменения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения события статуса.</returns>
    public Task StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken)
    {
        _statusEvents.Add(status);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сохраняет алерт.
    /// </summary>
    /// <param name="alert">Алерт.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения алерта.</returns>
    public Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken)
    {
        _alerts.Add(alert);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Загружает метаданные инструментов.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор метаданных инструментов.</returns>
    public Task<IReadOnlyCollection<InstrumentMetadata>> GetInstrumentsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)_instruments.ToArray());
    }

    /// <summary>
    /// Загружает конфигурации правил алертинга.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор конфигураций правил.</returns>
    public Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_alertRules);
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

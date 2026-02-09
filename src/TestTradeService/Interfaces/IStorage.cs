using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Абстракция слоя хранения данных торговой системы.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Сохраняет пакет сырых тиков вместе с исходным payload.
    /// </summary>
    /// <param name="rawTicks">Набор сырых тиков до нормализации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача пакетного сохранения сырых тиков.</returns>
    Task StoreRawTicksAsync(IReadOnlyCollection<RawTick> rawTicks, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет сырой тик вместе с исходным payload.
    /// </summary>
    /// <param name="rawTick">Сырой тик до нормализации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения сырого тика.</returns>
    Task StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет пакет нормализованных тиков в хранилище.
    /// </summary>
    /// <param name="ticks">Набор нормализованных тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача пакетного сохранения тиков.</returns>
    Task StoreTicksAsync(IReadOnlyCollection<NormalizedTick> ticks, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет raw-тик в хранилище.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения тика.</returns>
    Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет пакет агрегированных свечей.
    /// </summary>
    /// <param name="candles">Набор агрегированных свечей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача пакетного сохранения свечей.</returns>
    Task StoreAggregatesAsync(IReadOnlyCollection<AggregatedCandle> candles, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет агрегированную свечу.
    /// </summary>
    /// <param name="candle">Агрегированная свеча.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения свечи.</returns>
    Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет метаданные инструмента.
    /// </summary>
    /// <param name="metadata">Метаданные инструмента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения метаданных инструмента.</returns>
    Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет актуальный статус источника данных.
    /// </summary>
    /// <param name="status">Статус источника данных.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения статуса источника.</returns>
    Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет событие изменения статуса источника данных.
    /// </summary>
    /// <param name="status">Статус источника данных на момент изменения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения события статуса источника.</returns>
    Task StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет алерт.
    /// </summary>
    /// <param name="alert">Событие алертинга.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения алерта.</returns>
    Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken);

    /// <summary>
    /// Загружает метаданные активных инструментов.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор метаданных инструментов.</returns>
    Task<IReadOnlyCollection<InstrumentMetadata>> GetInstrumentsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Загружает конфигурации правил алертинга.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор конфигураций правил.</returns>
    Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken);
}

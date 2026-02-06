using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Abstractions;

/// <summary>
/// Контракт канала сбора рыночных данных.
/// </summary>
public interface IDataChannel : IAsyncDisposable
{
    /// <summary>
    /// Уникальный идентификатор канала.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Название биржи-источника.
    /// </summary>
    string Exchange { get; }

    /// <summary>
    /// Тип канала по транспорту.
    /// </summary>
    ChannelKind ChannelKind { get; }

    /// <summary>
    /// Набор инструментов, обслуживаемых каналом.
    /// </summary>
    IReadOnlyCollection<string> Symbols { get; }

    /// <summary>
    /// Текущее состояние жизненного цикла канала.
    /// </summary>
    ChannelLifecycleState LifecycleState { get; }

    /// <summary>
    /// Событие получения сырого сообщения.
    /// </summary>
    event Func<RawMessage, Task>? RawMessageReceived;

    /// <summary>
    /// Событие ошибки канала.
    /// </summary>
    event Func<Exception, Task>? ErrorOccurred;

    /// <summary>
    /// Событие изменения сетевого статуса источника.
    /// </summary>
    event Func<SourceConnectivityStatus, Task>? StatusChanged;

    /// <summary>
    /// Событие обновления статистики канала.
    /// </summary>
    event Func<ChannelStatistics, Task>? StatisticsUpdated;

    /// <summary>
    /// Запускает канал.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Останавливает канал с graceful shutdown.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

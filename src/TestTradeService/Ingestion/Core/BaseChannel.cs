using System.Threading;
using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Abstractions;
using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Core;

/// <summary>
/// Базовая реализация канала с управлением жизненным циклом и публикацией событий.
/// </summary>
public abstract class BaseChannel : IDataChannel
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly ILogger _logger;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;

    private long _incomingMessages;
    private long _errorCount;
    private long _reconnectCount;
    private DateTimeOffset? _lastMessageAt;
    private SourceConnectivityStatus _status = SourceConnectivityStatus.Offline;

    /// <summary>
    /// Инициализирует базовый канал.
    /// </summary>
    protected BaseChannel(string id, string exchange, ChannelKind channelKind, IReadOnlyCollection<string> symbols, ILogger logger)
    {
        Id = id;
        Exchange = exchange;
        ChannelKind = channelKind;
        Symbols = symbols;
        _logger = logger;
    }

    /// <summary>
    /// Уникальный идентификатор канала.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Название биржи.
    /// </summary>
    public string Exchange { get; }

    /// <summary>
    /// Тип канала по транспорту.
    /// </summary>
    public ChannelKind ChannelKind { get; }

    /// <summary>
    /// Символы канала.
    /// </summary>
    public IReadOnlyCollection<string> Symbols { get; }

    /// <summary>
    /// Текущее состояние жизненного цикла.
    /// </summary>
    public ChannelLifecycleState LifecycleState { get; private set; } = ChannelLifecycleState.Created;

    /// <inheritdoc />
    public event Func<RawMessage, Task>? RawMessageReceived;

    /// <inheritdoc />
    public event Func<Exception, Task>? ErrorOccurred;

    /// <inheritdoc />
    public event Func<SourceConnectivityStatus, Task>? StatusChanged;

    /// <inheritdoc />
    public event Func<ChannelStatistics, Task>? StatisticsUpdated;

    /// <summary>
    /// Запускает канал и переводит его в рабочее состояние.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (LifecycleState is ChannelLifecycleState.Starting or ChannelLifecycleState.Running)
                return;

            LifecycleState = ChannelLifecycleState.Starting;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(() => RunInternalAsync(_runCts.Token), CancellationToken.None);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Останавливает канал в режиме graceful shutdown.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_runCts is null || _runTask is null || LifecycleState is ChannelLifecycleState.Stopping or ChannelLifecycleState.Stopped)
                return;

            LifecycleState = ChannelLifecycleState.Stopping;
            _runCts.Cancel();
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (_runTask is not null)
            await _runTask.WaitAsync(cancellationToken);

        LifecycleState = ChannelLifecycleState.Stopped;
        await PublishStatusAsync(SourceConnectivityStatus.Offline);
    }

    /// <summary>
    /// Освобождает ресурсы канала.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _runCts?.Dispose();
        _lifecycleGate.Dispose();
    }

    /// <summary>
    /// Содержит транспорт-специфичную логику исполнения канала.
    /// </summary>
    protected abstract Task RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Публикует сырое сообщение и обновляет счётчики.
    /// </summary>
    protected async Task PublishRawMessageAsync(RawMessage message)
    {
        Interlocked.Increment(ref _incomingMessages);
        _lastMessageAt = message.ReceivedAt;

        if (RawMessageReceived is not null)
            await RawMessageReceived.Invoke(message);

        await PublishStatisticsAsync();
    }

    /// <summary>
    /// Публикует ошибку канала и обновляет счётчики.
    /// </summary>
    protected async Task PublishErrorAsync(Exception exception)
    {
        Interlocked.Increment(ref _errorCount);
        _logger.LogError(exception, "Channel {ChannelId} error", Id);

        if (ErrorOccurred is not null)
            await ErrorOccurred.Invoke(exception);

        await PublishStatisticsAsync();
    }

    /// <summary>
    /// Публикует изменение статуса источника.
    /// </summary>
    protected async Task PublishStatusAsync(SourceConnectivityStatus status)
    {
        _status = status;

        if (StatusChanged is not null)
            await StatusChanged.Invoke(status);

        await PublishStatisticsAsync();
    }

    /// <summary>
    /// Увеличивает счётчик переподключений.
    /// </summary>
    protected void IncrementReconnectCounter() => Interlocked.Increment(ref _reconnectCount);

    private async Task RunInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            LifecycleState = ChannelLifecycleState.Running;
            await PublishStatusAsync(SourceConnectivityStatus.Online);
            await RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Channel {ChannelId} canceled", Id);
        }
        catch (Exception ex)
        {
            LifecycleState = ChannelLifecycleState.Faulted;
            await PublishErrorAsync(ex);
            await PublishStatusAsync(SourceConnectivityStatus.Offline);
        }
    }

    private Task PublishStatisticsAsync()
    {
        if (StatisticsUpdated is null)
            return Task.CompletedTask;

        var snapshot = new ChannelStatistics
        {
            IncomingMessages = Interlocked.Read(ref _incomingMessages),
            ErrorCount = Interlocked.Read(ref _errorCount),
            ReconnectCount = Interlocked.Read(ref _reconnectCount),
            LastMessageAt = _lastMessageAt,
            Status = _status
        };

        return StatisticsUpdated.Invoke(snapshot);
    }
}

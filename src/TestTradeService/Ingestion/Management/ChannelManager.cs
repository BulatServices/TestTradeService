using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Abstractions;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Factory;
using TestTradeService.Ingestion.Models;
using TestTradeService.Ingestion.Monitoring;

namespace TestTradeService.Ingestion.Management;

/// <summary>
/// Оркестратор жизненного цикла набора каналов ingestion.
/// </summary>
public sealed class ChannelManager : IAsyncDisposable
{
    private readonly IChannelFactory _channelFactory;
    private readonly SourceHealthTracker _healthTracker;
    private readonly ILogger<ChannelManager> _logger;
    private readonly List<IDataChannel> _channels = [];

    /// <summary>
    /// Инициализирует менеджер каналов.
    /// </summary>
    public ChannelManager(IChannelFactory channelFactory, SourceHealthTracker healthTracker, ILogger<ChannelManager> logger)
    {
        _channelFactory = channelFactory;
        _healthTracker = healthTracker;
        _logger = logger;
    }

    /// <summary>
    /// Событие проксирования сырых сообщений от каналов.
    /// </summary>
    public event Func<RawMessage, Task>? RawMessageReceived;

    /// <summary>
    /// Текущий набор управляемых каналов.
    /// </summary>
    public IReadOnlyCollection<IDataChannel> Channels => _channels;

    /// <summary>
    /// Создаёт и регистрирует каналы по переданным конфигурациям.
    /// </summary>
    public void Initialize(IReadOnlyCollection<ChannelConfig> configs)
    {
        foreach (var config in configs)
        {
            var channel = _channelFactory.Create(config);
            Subscribe(channel);
            _channels.Add(channel);
        }
    }

    /// <summary>
    /// Запускает все зарегистрированные каналы.
    /// </summary>
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var channel in _channels)
            await channel.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Останавливает все зарегистрированные каналы.
    /// </summary>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var channel in _channels)
            await channel.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Возвращает агрегированное состояние здоровья источников.
    /// </summary>
    public ChannelStatisticsSummary GetHealthSummary() => _healthTracker.GetSummary();

    /// <summary>
    /// Освобождает ресурсы управляемых каналов.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels)
            await channel.DisposeAsync();
    }

    private void Subscribe(IDataChannel channel)
    {
        channel.RawMessageReceived += async message =>
        {
            if (RawMessageReceived is not null)
                await RawMessageReceived.Invoke(message);
        };

        channel.ErrorOccurred += error =>
        {
            _logger.LogError(error, "Channel {ChannelId} published error", channel.Id);
            return Task.CompletedTask;
        };

        channel.StatusChanged += status =>
        {
            _logger.LogInformation("Channel {ChannelId} status changed to {Status}", channel.Id, status);
            return Task.CompletedTask;
        };

        channel.StatisticsUpdated += statistics =>
        {
            _healthTracker.Update(channel.Id, statistics);
            return Task.CompletedTask;
        };
    }
}

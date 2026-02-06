using System.Collections.Concurrent;
using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Monitoring;

/// <summary>
/// Агрегатор статистики и health-состояния по всем каналам.
/// </summary>
public sealed class SourceHealthTracker
{
    private readonly ConcurrentDictionary<string, ChannelStatistics> _statsByChannel = new();

    /// <summary>
    /// Обновляет статистику канала по его идентификатору.
    /// </summary>
    public void Update(string channelId, ChannelStatistics statistics)
    {
        _statsByChannel[channelId] = statistics;
    }

    /// <summary>
    /// Возвращает снимок статистики по каналам.
    /// </summary>
    public IReadOnlyDictionary<string, ChannelStatistics> Snapshot() => _statsByChannel;

    /// <summary>
    /// Возвращает агрегированную сводку состояния всех каналов.
    /// </summary>
    public ChannelStatisticsSummary GetSummary()
    {
        var snapshot = _statsByChannel.Values;
        return new ChannelStatisticsSummary
        {
            TotalChannels = _statsByChannel.Count,
            OnlineChannels = snapshot.Count(s => s.Status == SourceConnectivityStatus.Online),
            ReconnectingChannels = snapshot.Count(s => s.Status == SourceConnectivityStatus.Reconnecting),
            OfflineChannels = snapshot.Count(s => s.Status == SourceConnectivityStatus.Offline),
            TotalMessages = snapshot.Sum(s => s.IncomingMessages),
            TotalErrors = snapshot.Sum(s => s.ErrorCount),
            TotalReconnects = snapshot.Sum(s => s.ReconnectCount)
        };
    }
}

/// <summary>
/// Сводные показатели по всем каналам источников.
/// </summary>
public sealed record ChannelStatisticsSummary
{
    /// <summary>
    /// Общее количество каналов.
    /// </summary>
    public required int TotalChannels { get; init; }

    /// <summary>
    /// Количество каналов в статусе online.
    /// </summary>
    public required int OnlineChannels { get; init; }

    /// <summary>
    /// Количество каналов в статусе reconnecting.
    /// </summary>
    public required int ReconnectingChannels { get; init; }

    /// <summary>
    /// Количество каналов в статусе offline.
    /// </summary>
    public required int OfflineChannels { get; init; }

    /// <summary>
    /// Суммарное количество входящих сообщений.
    /// </summary>
    public required long TotalMessages { get; init; }

    /// <summary>
    /// Суммарное количество ошибок.
    /// </summary>
    public required long TotalErrors { get; init; }

    /// <summary>
    /// Суммарное количество переподключений.
    /// </summary>
    public required long TotalReconnects { get; init; }
}

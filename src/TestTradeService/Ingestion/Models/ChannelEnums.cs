namespace TestTradeService.Ingestion.Models;

/// <summary>
/// Вид канала по транспорту.
/// </summary>
public enum ChannelKind
{
    Rest,
    WebSocket
}

/// <summary>
/// Вид рыночного потока.
/// </summary>
public enum StreamType
{
    Trades,
    Ticker,
    OrderBook
}

/// <summary>
/// Транспорт получения сообщения.
/// </summary>
public enum TransportType
{
    Rest,
    WebSocket
}

/// <summary>
/// Состояние жизненного цикла канала.
/// </summary>
public enum ChannelLifecycleState
{
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
    Faulted
}

/// <summary>
/// Сетевой статус доступности источника.
/// </summary>
public enum SourceConnectivityStatus
{
    Offline,
    Online,
    Reconnecting
}

namespace TestTradeService.Models;

/// <summary>
/// Транспорт/тип подключения источника рыночных данных.
/// </summary>
public enum MarketDataSourceTransport
{
    /// <summary>
    /// Неизвестно или не задано.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Получение данных через REST (HTTP) запросы, как правило, в режиме polling.
    /// </summary>
    Rest = 1,

    /// <summary>
    /// Получение данных через WebSocket-подключение в режиме стрима.
    /// </summary>
    WebSocket = 2
}


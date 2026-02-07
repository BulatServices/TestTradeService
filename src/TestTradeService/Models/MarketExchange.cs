namespace TestTradeService.Models;

/// <summary>
/// Биржа (торговая площадка), к которой относятся рыночные данные.
/// </summary>
public enum MarketExchange
{
    /// <summary>
    /// Неизвестно или не задано.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Демонстрационная/тестовая площадка (данные генерируются внутри сервиса).
    /// </summary>
    Demo = 1,

    /// <summary>
    /// Биржа Kraken.
    /// </summary>
    Kraken = 2,

    /// <summary>
    /// Биржа Coinbase (public Exchange API).
    /// </summary>
    Coinbase = 3,

    /// <summary>
    /// Биржа Bybit.
    /// </summary>
    Bybit = 4
}

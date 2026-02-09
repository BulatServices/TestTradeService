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
    /// Биржа Kraken.
    /// </summary>
    Kraken = 1,

    /// <summary>
    /// Биржа Coinbase (public Exchange API).
    /// </summary>
    Coinbase = 2,

    /// <summary>
    /// Биржа Bybit.
    /// </summary>
    Bybit = 3
}

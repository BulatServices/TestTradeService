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
    Demo = 1
}


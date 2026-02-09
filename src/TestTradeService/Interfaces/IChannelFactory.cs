using System.Threading.Channels;
using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Контракт фабрики каналов для потоковой обработки данных.
/// </summary>
public interface IChannelFactory
{
    /// <summary>
    /// Создает bounded-канал для сырых тиков.
    /// </summary>
    /// <param name="capacity">Емкость канала.</param>
    /// <returns>Созданный канал тиков.</returns>
    Channel<Tick> CreateTickChannel(int capacity = 10_000);

    /// <summary>
    /// Создает bounded-канал для нормализованных тиков.
    /// </summary>
    /// <param name="capacity">Емкость канала.</param>
    /// <returns>Созданный канал нормализованных тиков.</returns>
    Channel<NormalizedTick> CreateNormalizedChannel(int capacity = 10_000);
}

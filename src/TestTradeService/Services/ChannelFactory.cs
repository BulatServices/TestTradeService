using System.Threading.Channels;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Фабрика каналов для потоковой обработки данных.
/// </summary>
public sealed class ChannelFactory : IChannelFactory
{
    /// <summary>
    /// Создает bounded-канал для сырых тиков.
    /// </summary>
    public Channel<Tick> CreateTickChannel(int capacity = 10_000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        return Channel.CreateBounded<Tick>(options);
    }

    /// <summary>
    /// Создает bounded-канал для нормализованных тиков.
    /// </summary>
    public Channel<NormalizedTick> CreateNormalizedChannel(int capacity = 10_000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        return Channel.CreateBounded<NormalizedTick>(options);
    }
}

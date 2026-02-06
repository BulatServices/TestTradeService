using TestTradeService.Ingestion.Abstractions;
using TestTradeService.Ingestion.Configuration;

namespace TestTradeService.Ingestion.Factory;

/// <summary>
/// Фабрика создания каналов ingestion.
/// </summary>
public interface IChannelFactory
{
    /// <summary>
    /// Создаёт канал по конфигурации.
    /// </summary>
    IDataChannel Create(ChannelConfig config);
}

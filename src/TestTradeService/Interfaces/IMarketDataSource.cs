using System.Threading.Channels;
using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Определяет источник рыночных данных (REST/WebSocket и т.д.).
/// </summary>
public interface IMarketDataSource
{
    /// <summary>
    /// Возвращает имя источника данных.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Запускает источник и публикует тики в канал.
    /// </summary>
    /// <param name="writer">Канал для записи входящих тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken);
}

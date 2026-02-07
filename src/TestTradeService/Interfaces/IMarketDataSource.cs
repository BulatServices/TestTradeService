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
    /// Возвращает биржу (торговую площадку), к которой относится источник.
    /// </summary>
    MarketExchange Exchange { get; }

    /// <summary>
    /// Возвращает транспорт/тип подключения источника (REST/WebSocket и т.д.).
    /// </summary>
    MarketDataSourceTransport Transport { get; }

    /// <summary>
    /// Запускает источник и публикует тики в канал.
    /// Метод, как правило, является долгоживущим и завершается при отмене <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="writer">Канал для записи входящих тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken);

    /// <summary>
    /// Запрашивает у источника корректную остановку (закрытие соединений, отписка и т.п.).
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task StopAsync(CancellationToken cancellationToken);
}

using System.Threading.Channels;
using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Контракт конвейера обработки рыночных тиков.
/// </summary>
public interface IDataPipeline
{
    /// <summary>
    /// Возвращает количество тиков, считанных конвейером из входного канала.
    /// </summary>
    long ConsumedTickCount { get; }

    /// <summary>
    /// Запускает чтение тиков из канала и их последовательную обработку.
    /// </summary>
    /// <param name="reader">Канал для чтения тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения конвейера.</returns>
    Task StartAsync(ChannelReader<Tick> reader, CancellationToken cancellationToken);
}

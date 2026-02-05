using TestTradeService.Models;

namespace TestTradeService.Interfaces;

/// <summary>
/// Канал доставки уведомлений об алертах.
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Имя канала уведомления.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Отправляет уведомление о сработавшем алерте.
    /// </summary>
    /// <param name="alert">Алерт для отправки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task NotifyAsync(Alert alert, CancellationToken cancellationToken);
}

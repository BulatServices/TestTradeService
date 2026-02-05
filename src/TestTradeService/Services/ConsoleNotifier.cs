using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Канал уведомлений в консоль/лог.
/// </summary>
public sealed class ConsoleNotifier : INotifier
{
    private readonly ILogger<ConsoleNotifier> _logger;

    /// <summary>
    /// Инициализирует notifier для консоли.
    /// </summary>
    public ConsoleNotifier(ILogger<ConsoleNotifier> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Имя канала уведомления.
    /// </summary>
    public string Name => "Console";

    /// <summary>
    /// Отправляет алерт в консоль.
    /// </summary>
    public Task NotifyAsync(Alert alert, CancellationToken cancellationToken)
    {
        _logger.LogWarning("ALERT {Rule} for {Symbol}: {Message}", alert.Rule, alert.Symbol, alert.Message);
        return Task.CompletedTask;
    }
}

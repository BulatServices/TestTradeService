using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Заглушка email-уведомлений.
/// </summary>
public sealed class EmailStubNotifier : INotifier
{
    private readonly ILogger<EmailStubNotifier> _logger;

    /// <summary>
    /// Инициализирует email-заглушку.
    /// </summary>
    public EmailStubNotifier(ILogger<EmailStubNotifier> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Имя канала уведомления.
    /// </summary>
    public string Name => "EmailStub";

    /// <summary>
    /// Логирует факт отправки email (без реальной отправки).
    /// </summary>
    public Task NotifyAsync(Alert alert, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email stub: {Rule} -> {Message}", alert.Rule, alert.Message);
        return Task.CompletedTask;
    }
}

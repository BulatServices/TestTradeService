using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Оркестратор правил алертинга и каналов доставки уведомлений.
/// </summary>
public sealed class AlertingService
{
    private readonly IEnumerable<IAlertRule> _rules;
    private readonly IEnumerable<INotifier> _notifiers;
    private readonly IStorage _storage;
    private readonly ILogger<AlertingService> _logger;

    /// <summary>
    /// Инициализирует сервис алертинга.
    /// </summary>
    public AlertingService(
        IEnumerable<IAlertRule> rules,
        IEnumerable<INotifier> notifiers,
        IStorage storage,
        ILogger<AlertingService> logger)
    {
        _rules = rules;
        _notifiers = notifiers;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Проверяет правила по текущему тику и отправляет алерты в каналы уведомлений.
    /// </summary>
    public async Task<IReadOnlyCollection<Alert>> HandleAsync(NormalizedTick tick, MetricsSnapshot metrics, CancellationToken cancellationToken)
    {
        var emittedAlerts = new List<Alert>();

        foreach (var rule in _rules)
        {
            if (!rule.IsMatch(tick, metrics))
            {
                continue;
            }

            var alert = rule.CreateAlert(tick, metrics);
            emittedAlerts.Add(alert);
            await _storage.StoreAlertAsync(alert, cancellationToken);

            foreach (var notifier in _notifiers)
            {
                try
                {
                    await notifier.NotifyAsync(alert, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Notifier {Notifier} failed", notifier.Name);
                }
            }
        }

        return emittedAlerts;
    }
}

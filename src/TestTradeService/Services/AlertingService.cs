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
    private readonly IReadOnlyDictionary<string, INotifier> _notifiersByName;
    private readonly IReadOnlyCollection<INotifier> _allNotifiers;
    private readonly IAlertRuleConfigProvider _configProvider;
    private readonly IStorage _storage;
    private readonly ILogger<AlertingService> _logger;

    /// <summary>
    /// Инициализирует сервис алертинга.
    /// </summary>
    /// <param name="rules">Набор правил алертинга.</param>
    /// <param name="notifiers">Набор каналов уведомлений.</param>
    /// <param name="configProvider">Провайдер конфигурации правил алертинга.</param>
    /// <param name="storage">Хранилище алертов.</param>
    /// <param name="logger">Логгер сервиса алертинга.</param>
    public AlertingService(
        IEnumerable<IAlertRule> rules,
        IEnumerable<INotifier> notifiers,
        IAlertRuleConfigProvider configProvider,
        IStorage storage,
        ILogger<AlertingService> logger)
    {
        _rules = rules;
        _configProvider = configProvider;
        _storage = storage;
        _logger = logger;

        var notifierArray = notifiers.ToArray();
        _allNotifiers = notifierArray;
        _notifiersByName = notifierArray
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет правила по текущему тику и отправляет алерты в каналы уведомлений.
    /// </summary>
    /// <param name="tick">Текущий нормализованный тик.</param>
    /// <param name="metrics">Текущие метрики по инструменту.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список сформированных алертов.</returns>
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

            var selectedNotifiers = SelectNotifiers(rule.Name, tick.Source, tick.Symbol);
            foreach (var notifier in selectedNotifiers)
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

    private IReadOnlyCollection<INotifier> SelectNotifiers(string ruleName, string source, string symbol)
    {
        var ruleParameters = _configProvider.GetParameters(ruleName, source, symbol);
        var perRuleChannels = AlertingChannels.ParseCsv(ruleParameters.GetValueOrDefault(AlertingChannels.ChannelsParameterKey));

        IReadOnlyCollection<string> channelNames;
        if (perRuleChannels.Count > 0)
        {
            channelNames = perRuleChannels;
        }
        else
        {
            var globalParameters = _configProvider.GetParameters(AlertingChannels.GlobalRuleName, source, symbol);
            var globalChannels = AlertingChannels.ParseCsv(globalParameters.GetValueOrDefault(AlertingChannels.ChannelsParameterKey));
            channelNames = globalChannels.Count > 0 ? globalChannels : Array.Empty<string>();
        }

        if (channelNames.Count == 0)
        {
            return _allNotifiers;
        }

        var selected = new List<INotifier>();
        foreach (var channel in channelNames)
        {
            if (!AlertingChannels.IsKnown(channel))
            {
                _logger.LogWarning("Unknown notifier channel in configuration: {Channel}", channel);
                continue;
            }

            if (!_notifiersByName.TryGetValue(channel, out var notifier))
            {
                _logger.LogWarning("Notifier channel {Channel} is configured but not registered", channel);
                continue;
            }

            selected.Add(notifier);
        }

        return selected;
    }
}

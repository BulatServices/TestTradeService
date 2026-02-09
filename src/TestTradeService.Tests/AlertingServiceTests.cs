using Microsoft.Extensions.Logging.Abstractions;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Services;
using TestTradeService.Storage;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты сервиса алертинга.
/// </summary>
public sealed class AlertingServiceTests
{
    /// <summary>
    /// Проверяет, что при отсутствии конфигурации каналов алерт отправляется во все зарегистрированные notifier-ы.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoChannelConfig_UsesAllNotifiers()
    {
        var rule = new StubAlertRule("Rule", true);
        var notifier1 = new CapturingNotifier("Console");
        var notifier2 = new CapturingNotifier("File");
        var storage = new CapturingStorage();
        var service = CreateService(new[] { rule }, new INotifier[] { notifier1, notifier2 }, Array.Empty<AlertRuleConfig>(), storage);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Single(storage.StoredAlerts);
        Assert.Single(notifier1.SentAlerts);
        Assert.Single(notifier2.SentAlerts);
    }

    /// <summary>
    /// Проверяет, что per-rule каналы имеют приоритет над глобальными каналами.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPerRuleChannelsConfigured_UsesPerRuleChannels()
    {
        var rule = new StubAlertRule("Rule", true);
        var console = new CapturingNotifier("Console");
        var file = new CapturingNotifier("File");
        var email = new CapturingNotifier("EmailStub");
        var storage = new CapturingStorage();
        var configs = new[]
        {
            new AlertRuleConfig
            {
                RuleName = AlertingChannels.GlobalRuleName,
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AlertingChannels.ChannelsParameterKey] = "Console,File"
                }
            },
            new AlertRuleConfig
            {
                RuleName = "Rule",
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AlertingChannels.ChannelsParameterKey] = "EmailStub"
                }
            }
        };

        var service = CreateService(new[] { rule }, new INotifier[] { console, file, email }, configs, storage);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Empty(console.SentAlerts);
        Assert.Empty(file.SentAlerts);
        Assert.Single(email.SentAlerts);
    }

    /// <summary>
    /// Проверяет, что глобальные каналы применяются, если per-rule переопределение отсутствует.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenOnlyGlobalChannelsConfigured_UsesGlobalChannels()
    {
        var rule = new StubAlertRule("Rule", true);
        var console = new CapturingNotifier("Console");
        var file = new CapturingNotifier("File");
        var storage = new CapturingStorage();
        var configs = new[]
        {
            new AlertRuleConfig
            {
                RuleName = AlertingChannels.GlobalRuleName,
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AlertingChannels.ChannelsParameterKey] = "File"
                }
            }
        };

        var service = CreateService(new[] { rule }, new INotifier[] { console, file }, configs, storage);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Empty(console.SentAlerts);
        Assert.Single(file.SentAlerts);
    }

    /// <summary>
    /// Проверяет, что неизвестные каналы игнорируются и не прерывают отправку в валидные каналы.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenUnknownChannelConfigured_UsesKnownChannelsOnly()
    {
        var rule = new StubAlertRule("Rule", true);
        var file = new CapturingNotifier("File");
        var storage = new CapturingStorage();
        var configs = new[]
        {
            new AlertRuleConfig
            {
                RuleName = AlertingChannels.GlobalRuleName,
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AlertingChannels.ChannelsParameterKey] = "File,UnknownChannel"
                }
            }
        };

        var service = CreateService(new[] { rule }, new INotifier[] { file }, configs, storage);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Single(file.SentAlerts);
    }

    /// <summary>
    /// Проверяет, что исключения в notifier-е не прерывают обработку остальных каналов.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ContinuesToOtherNotifiers()
    {
        var rule = new StubAlertRule("Rule", true);
        var throwing = new ThrowingNotifier("Console");
        var good = new CapturingNotifier("File");
        var storage = new CapturingStorage();
        var configs = new[]
        {
            new AlertRuleConfig
            {
                RuleName = AlertingChannels.GlobalRuleName,
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AlertingChannels.ChannelsParameterKey] = "Console,File"
                }
            }
        };

        var service = CreateService(new[] { rule }, new INotifier[] { throwing, good }, configs, storage);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Single(storage.StoredAlerts);
        Assert.Single(good.SentAlerts);
    }

    /// <summary>
    /// Проверяет, что при отсутствии совпадений правила алерт не формируется.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenRuleDoesNotMatch_SkipsAlert()
    {
        var rule = new StubAlertRule("Rule", false);
        var storage = new CapturingStorage();
        var notifier = new CapturingNotifier("File");
        var service = CreateService(new[] { rule }, new INotifier[] { notifier }, Array.Empty<AlertRuleConfig>(), storage);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Empty(storage.StoredAlerts);
        Assert.Empty(notifier.SentAlerts);
    }

    private static AlertingService CreateService(
        IEnumerable<IAlertRule> rules,
        IEnumerable<INotifier> notifiers,
        IReadOnlyCollection<AlertRuleConfig> configs,
        IStorage storage)
    {
        var provider = new AlertRuleConfigProvider(configs);
        return new AlertingService(rules, notifiers, provider, storage, NullLogger<AlertingService>.Instance);
    }

    private static NormalizedTick CreateTick()
    {
        return new NormalizedTick
        {
            Source = "Coinbase-WebSocket",
            Symbol = "BTC-USD",
            Price = 20_000m,
            Volume = 1m,
            Timestamp = DateTimeOffset.UtcNow,
            Fingerprint = "fp"
        };
    }

    private static MetricsSnapshot CreateMetrics()
    {
        return new MetricsSnapshot
        {
            Symbol = "BTC-USD",
            WindowStart = DateTimeOffset.UtcNow,
            Window = TimeSpan.FromMinutes(1),
            AveragePrice = 20_000m,
            Volatility = 0.5m,
            Count = 10,
            AverageVolume = 0.5m
        };
    }

    private sealed class StubAlertRule : IAlertRule
    {
        public StubAlertRule(string name, bool match)
        {
            Name = name;
            Match = match;
        }

        public string Name { get; }

        public bool Match { get; }

        public bool IsMatch(NormalizedTick tick, MetricsSnapshot metrics)
        {
            return Match;
        }

        public Alert CreateAlert(NormalizedTick tick, MetricsSnapshot metrics)
        {
            return new Alert
            {
                Rule = Name,
                Source = tick.Source,
                Symbol = tick.Symbol,
                Message = "Alert",
                Timestamp = tick.Timestamp
            };
        }
    }

    private sealed class CapturingNotifier : INotifier
    {
        private readonly List<Alert> _alerts = new();

        public CapturingNotifier(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public IReadOnlyList<Alert> SentAlerts => _alerts;

        public Task NotifyAsync(Alert alert, CancellationToken cancellationToken)
        {
            _alerts.Add(alert);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotifier : INotifier
    {
        public ThrowingNotifier(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Task NotifyAsync(Alert alert, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Notifier failure");
        }
    }

    private sealed class CapturingStorage : IStorage
    {
        private readonly List<Alert> _alerts = new();

        public IReadOnlyList<Alert> StoredAlerts => _alerts;

        public Task StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreSourceStatusEventAsync(SourceStatus status, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken)
        {
            _alerts.Add(alert);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<InstrumentMetadata>> GetInstrumentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<InstrumentMetadata>)Array.Empty<InstrumentMetadata>());
        }

        public Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyCollection<AlertRuleConfig>)Array.Empty<AlertRuleConfig>());
        }
    }
}

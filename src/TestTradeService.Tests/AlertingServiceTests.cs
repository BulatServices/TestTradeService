using Microsoft.Extensions.Logging.Abstractions;
using TestTradeService.Interfaces;
using TestTradeService.Models;
using TestTradeService.Services;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты сервиса алертинга.
/// </summary>
public sealed class AlertingServiceTests
{
    /// <summary>
    /// Проверяет, что алерт сохраняется и отправляется во все нотификаторы при совпадении правила.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenRuleMatches_StoresAlertAndNotifies()
    {
        var rule = new StubAlertRule("Rule", true);
        var notifier1 = new CapturingNotifier("first");
        var notifier2 = new CapturingNotifier("second");
        var storage = new CapturingStorage();
        var service = new AlertingService(
            new[] { rule },
            new INotifier[] { notifier1, notifier2 },
            storage,
            NullLogger<AlertingService>.Instance);

        var tick = CreateTick();
        var metrics = CreateMetrics();

        await service.HandleAsync(tick, metrics, CancellationToken.None);

        Assert.Single(storage.StoredAlerts);
        Assert.Single(notifier1.SentAlerts);
        Assert.Single(notifier2.SentAlerts);
        Assert.Equal("Rule", storage.StoredAlerts[0].Rule);
    }

    /// <summary>
    /// Проверяет, что исключения в нотификаторе не прерывают обработку остальных каналов.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ContinuesToOtherNotifiers()
    {
        var rule = new StubAlertRule("Rule", true);
        var throwing = new ThrowingNotifier("bad");
        var good = new CapturingNotifier("good");
        var storage = new CapturingStorage();
        var service = new AlertingService(
            new[] { rule },
            new INotifier[] { throwing, good },
            storage,
            NullLogger<AlertingService>.Instance);

        var tick = CreateTick();
        var metrics = CreateMetrics();

        await service.HandleAsync(tick, metrics, CancellationToken.None);

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
        var notifier = new CapturingNotifier("good");
        var service = new AlertingService(
            new[] { rule },
            new INotifier[] { notifier },
            storage,
            NullLogger<AlertingService>.Instance);

        await service.HandleAsync(CreateTick(), CreateMetrics(), CancellationToken.None);

        Assert.Empty(storage.StoredAlerts);
        Assert.Empty(notifier.SentAlerts);
    }

    private static NormalizedTick CreateTick()
    {
        return new NormalizedTick
        {
            Source = "Coinbase",
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
            Count = 10
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

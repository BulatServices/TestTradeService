using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Источник рыночных данных через периодический REST polling.
/// </summary>
public sealed class RestPollingSource : IMarketDataSource
{
    private readonly ILogger<RestPollingSource> _logger;

    /// <summary>
    /// Инициализирует источник REST polling.
    /// </summary>
    public RestPollingSource(ILogger<RestPollingSource> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Имя источника.
    /// </summary>
    public string Name => "RestApi";

    /// <summary>
    /// Запускает генерацию/чтение тиков и отправляет их в канал.
    /// </summary>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var symbols = new[] { "BTC-USD", "ETH-USD", "SOL-USD" };
        var random = Random.Shared;

        _logger.LogInformation("REST polling source started");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var symbol in symbols)
            {
                var tick = new Tick
                {
                    Source = Name,
                    Symbol = symbol,
                    Price = 20_000m + (decimal)random.NextDouble() * 2_000m,
                    Volume = 0.1m + (decimal)random.NextDouble() * 5m,
                    Timestamp = now,
                    TradeId = $"rest-{now:O}-{random.Next(1_000_000)}"
                };

                await writer.WriteAsync(tick, cancellationToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        writer.TryComplete();
    }
}

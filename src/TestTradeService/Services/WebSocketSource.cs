using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Источник рыночных данных через WebSocket-поток.
/// </summary>
public sealed class WebSocketSource : IMarketDataSource
{
    private readonly ILogger<WebSocketSource> _logger;

    /// <summary>
    /// Инициализирует источник WebSocket.
    /// </summary>
    public WebSocketSource(ILogger<WebSocketSource> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Имя источника.
    /// </summary>
    public string Name => "WebSocket";

    /// <summary>
    /// Запускает потоковую публикацию тиков в канал.
    /// </summary>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var symbols = new[] { "BTC-USD", "ETH-USD", "XRP-USD" };
        var random = Random.Shared;

        _logger.LogInformation("WebSocket source started");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var symbol = symbols[random.Next(symbols.Length)];
            var tick = new Tick
            {
                Source = Name,
                Symbol = symbol,
                Price = 20_000m + (decimal)random.NextDouble() * 2_500m,
                Volume = 0.05m + (decimal)random.NextDouble() * 3m,
                Timestamp = now,
                TradeId = $"ws-{now:O}-{random.Next(1_000_000)}"
            };

            await writer.WriteAsync(tick, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        writer.TryComplete();
    }
}

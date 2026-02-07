using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Источник рыночных данных через WebSocket-поток.
/// </summary>
public sealed class WebSocketSource : IMarketDataSource
{
    private readonly ILogger<WebSocketSource> _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;

    /// <summary>
    /// Инициализирует источник WebSocket.
    /// </summary>
    /// <param name="logger">Логгер источника.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    public WebSocketSource(ILogger<WebSocketSource> logger, MarketInstrumentsConfig instrumentsConfig)
    {
        _logger = logger;
        _instrumentsConfig = instrumentsConfig;
    }

    /// <summary>
    /// Имя источника.
    /// </summary>
    public string Name => "WebSocket";

    /// <summary>
    /// Запускает потоковую публикацию тиков в канал.
    /// </summary>
    /// <param name="writer">Канал для публикации тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения потоковой публикации.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(MarketType.Perp);
        if (profile is null)
        {
            _logger.LogWarning("Не настроены инструменты для рынка perp. WebSocket-поток остановлен.");
            writer.TryComplete();
            return;
        }

        var symbols = profile.Symbols.ToArray();
        var tickInterval = profile.TargetUpdateInterval;
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
            await Task.Delay(tickInterval, cancellationToken);
        }

        writer.TryComplete();
    }
}

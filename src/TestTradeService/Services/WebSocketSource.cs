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
    private readonly IStorage _storage;

    /// <summary>
    /// Инициализирует источник WebSocket.
    /// </summary>
    /// <param name="logger">Логгер источника.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    /// <param name="storage">Слой хранения.</param>
    public WebSocketSource(ILogger<WebSocketSource> logger, MarketInstrumentsConfig instrumentsConfig, IStorage storage)
    {
        _logger = logger;
        _instrumentsConfig = instrumentsConfig;
        _storage = storage;
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

        var symbols = profile.Symbols?.ToArray() ?? Array.Empty<string>();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("Профиль perp не содержит символов. WebSocket-поток остановлен.");
            writer.TryComplete();
            return;
        }
        var tickInterval = profile.TargetUpdateInterval;
        var random = Random.Shared;

        await StoreInstrumentMetadataAsync(profile, cancellationToken);

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

    private async Task StoreInstrumentMetadataAsync(MarketInstrumentProfile profile, CancellationToken cancellationToken)
    {
        foreach (var symbol in profile.Symbols)
        {
            var (baseAsset, quoteAsset) = ParseSymbol(symbol);
            await _storage.StoreInstrumentAsync(new InstrumentMetadata
            {
                Exchange = Name,
                MarketType = profile.MarketType,
                Symbol = symbol,
                BaseAsset = baseAsset,
                QuoteAsset = quoteAsset,
                Description = $"{symbol} ({profile.MarketType})",
                PriceTickSize = 0.1m,
                VolumeStep = 0.001m,
                PriceDecimals = 1,
                VolumeDecimals = 3,
                ContractSize = 1m,
                MinNotional = 10m
            }, cancellationToken);
        }
    }

    private static (string BaseAsset, string QuoteAsset) ParseSymbol(string symbol)
    {
        var parts = symbol
            .Split(new[] { '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            2 => (parts[0], parts[1]),
            _ => (symbol, "UNKNOWN")
        };
    }
}

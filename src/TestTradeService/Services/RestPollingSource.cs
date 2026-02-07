using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Источник рыночных данных через периодический REST polling.
/// </summary>
public sealed class RestPollingSource : IMarketDataSource
{
    private readonly ILogger<RestPollingSource> _logger;
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IStorage _storage;

    /// <summary>
    /// Инициализирует источник REST polling.
    /// </summary>
    /// <param name="logger">Логгер источника.</param>
    /// <param name="instrumentsConfig">Конфигурация инструментов.</param>
    /// <param name="storage">Слой хранения.</param>
    public RestPollingSource(ILogger<RestPollingSource> logger, MarketInstrumentsConfig instrumentsConfig, IStorage storage)
    {
        _logger = logger;
        _instrumentsConfig = instrumentsConfig;
        _storage = storage;
    }

    /// <summary>
    /// Имя источника.
    /// </summary>
    public string Name => "RestApi";

    /// <summary>
    /// Запускает генерацию/чтение тиков и отправляет их в канал.
    /// </summary>
    /// <param name="writer">Канал для публикации тиков.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения цикла polling.</returns>
    public async Task StartAsync(ChannelWriter<Tick> writer, CancellationToken cancellationToken)
    {
        var profile = _instrumentsConfig.GetProfile(MarketType.Spot);
        if (profile is null)
        {
            _logger.LogWarning("Не настроены инструменты для рынка spot. REST polling остановлен.");
            writer.TryComplete();
            return;
        }

        var symbols = profile.Symbols;
        var pollInterval = profile.TargetUpdateInterval;
        var random = Random.Shared;

        await StoreInstrumentMetadataAsync(profile, cancellationToken);

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

            await Task.Delay(pollInterval, cancellationToken);
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
                PriceTickSize = 0.01m,
                VolumeStep = 0.0001m,
                PriceDecimals = 2,
                VolumeDecimals = 4,
                ContractSize = null,
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

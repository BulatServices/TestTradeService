using TestTradeService.Models;
using TestTradeService.Storage;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты in-memory хранилища.
/// </summary>
public sealed class InMemoryStorageTests
{
    /// <summary>
    /// Проверяет успешное сохранение валидных метаданных.
    /// </summary>
    [Fact]
    public async Task StoreInstrumentAsync_WithValidMetadata_Completes()
    {
        var storage = new InMemoryStorage();

        var metadata = new InstrumentMetadata
        {
            Exchange = "Binance",
            Symbol = "BTCUSDT",
            BaseAsset = "BTC",
            QuoteAsset = "USDT",
            Description = "BTC/USDT Spot",
            PriceTickSize = 0.1m,
            VolumeStep = 0.01m,
            PriceDecimals = 1,
            VolumeDecimals = 2,
            MarketType = MarketType.Spot,
            ContractSize = null,
            MinNotional = 10m
        };

        await storage.StoreInstrumentAsync(metadata, CancellationToken.None);
    }

    /// <summary>
    /// Проверяет, что invalid данные метаданных приводят к исключению.
    /// </summary>
    [Fact]
    public async Task StoreInstrumentAsync_WithInvalidMetadata_Throws()
    {
        var storage = new InMemoryStorage();

        var metadata = new InstrumentMetadata
        {
            Exchange = "",
            Symbol = "BTCUSDT",
            BaseAsset = "BTC",
            QuoteAsset = "USDT",
            Description = "Invalid",
            PriceTickSize = 0.1m,
            VolumeStep = 0.01m,
            PriceDecimals = 1,
            VolumeDecimals = 2,
            MarketType = MarketType.Spot
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => storage.StoreInstrumentAsync(metadata, CancellationToken.None));

        Assert.Contains("Exchange", exception.Message);
    }

    /// <summary>
    /// Проверяет, что для perp требуется ContractSize, а для spot он запрещен.
    /// </summary>
    [Fact]
    public async Task StoreInstrumentAsync_WhenMarketTypeRequiresContractSize_EnforcesRules()
    {
        var storage = new InMemoryStorage();

        var perpMetadata = new InstrumentMetadata
        {
            Exchange = "Bybit",
            Symbol = "BTCUSDT",
            BaseAsset = "BTC",
            QuoteAsset = "USDT",
            Description = "BTC/USDT Perp",
            PriceTickSize = 0.1m,
            VolumeStep = 0.01m,
            PriceDecimals = 1,
            VolumeDecimals = 2,
            MarketType = MarketType.Perp,
            ContractSize = null
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.StoreInstrumentAsync(perpMetadata, CancellationToken.None));

        var spotMetadata = perpMetadata with
        {
            MarketType = MarketType.Spot,
            ContractSize = 1m
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.StoreInstrumentAsync(spotMetadata, CancellationToken.None));
    }

    /// <summary>
    /// Проверяет, что методы сохранения raw-тиков и событий статуса выполняются без ошибок.
    /// </summary>
    [Fact]
    public async Task StoreRawTickAndStatusEventAsync_Completes()
    {
        var storage = new InMemoryStorage();

        await storage.StoreRawTickAsync(new RawTick
        {
            Exchange = "Bybit",
            Source = "Bybit-WebSocket",
            Symbol = "BTCUSDT",
            MarketType = "Spot",
            Price = 100m,
            Volume = 1m,
            TradeId = "t-1",
            EventTimestamp = DateTimeOffset.UtcNow,
            ReceivedAt = DateTimeOffset.UtcNow,
            Payload = "{\"symbol\":\"BTCUSDT\"}",
            Metadata = new Dictionary<string, string> { ["transport"] = "WebSocket" }
        }, CancellationToken.None);

        await storage.StoreSourceStatusEventAsync(new SourceStatus
        {
            Exchange = MarketExchange.Bybit,
            Source = "Bybit-WebSocket",
            IsOnline = true,
            LastUpdate = DateTimeOffset.UtcNow,
            Message = null
        }, CancellationToken.None);
    }
}

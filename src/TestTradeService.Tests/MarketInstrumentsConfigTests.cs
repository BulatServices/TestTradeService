using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты конфигурации инструментов.
/// </summary>
public sealed class MarketInstrumentsConfigTests
{
    /// <summary>
    /// Проверяет объединение символов без повторов.
    /// </summary>
    [Fact]
    public void GetAllSymbols_ReturnsDistinctSymbols()
    {
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Spot,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "BTCUSDT", "ETHUSDT" }
                },
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Bybit,
                    MarketType = MarketType.Perp,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "ETHUSDT", "BTCUSDT" }
                }
            }
        };

        var symbols = config.GetAllSymbols();

        Assert.Equal(2, symbols.Count);
        Assert.Contains("BTCUSDT", symbols);
        Assert.Contains("ETHUSDT", symbols);
    }

    /// <summary>
    /// Проверяет поиск профиля по бирже и типу рынка.
    /// </summary>
    [Fact]
    public void GetProfile_ReturnsMatchingProfile()
    {
        var spotProfile = new MarketInstrumentProfile
        {
            Exchange = MarketExchange.Coinbase,
            MarketType = MarketType.Spot,
            Transport = MarketDataSourceTransport.WebSocket,
            Symbols = new[] { "BTC-USD" }
        };
        var config = new MarketInstrumentsConfig
        {
            Profiles = new[]
            {
                spotProfile,
                new MarketInstrumentProfile
                {
                    Exchange = MarketExchange.Coinbase,
                    MarketType = MarketType.Perp,
                    Transport = MarketDataSourceTransport.WebSocket,
                    Symbols = new[] { "ETH-USD" }
                }
            }
        };

        var profile = config.GetProfile(MarketExchange.Coinbase, MarketType.Spot);

        Assert.Same(spotProfile, profile);
    }
}

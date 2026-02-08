using TestTradeService.Ingestion.Configuration;
using TestTradeService.Models;

namespace TestTradeService.Storage;

/// <summary>
/// Построитель fallback-конфигурации по умолчанию.
/// </summary>
public static class DefaultConfigurationFactory
{
    /// <summary>
    /// Формирует конфигурацию инструментов по умолчанию.
    /// </summary>
    /// <param name="demoMode">Признак демо-режима.</param>
    /// <returns>Конфигурация инструментов.</returns>
    public static MarketInstrumentsConfig CreateInstruments(bool demoMode)
    {
        return demoMode
            ? new MarketInstrumentsConfig
            {
                Profiles =
                [
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Demo,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.Rest,
                        Symbols = new[] { "BTC-USD", "ETH-USD", "SOL-USD" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Demo,
                        MarketType = MarketType.Perp,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "BTC-USD", "ETH-USD", "XRP-USD" },
                        TargetUpdateInterval = TimeSpan.FromMilliseconds(100)
                    }
                ]
            }
            : new MarketInstrumentsConfig
            {
                Profiles =
                [
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Kraken,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "XBT/USD", "ETH/USD" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Coinbase,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "BTC-USD", "ETH-USD", "SOL-USD" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    },
                    new MarketInstrumentProfile
                    {
                        Exchange = MarketExchange.Bybit,
                        MarketType = MarketType.Spot,
                        Transport = MarketDataSourceTransport.WebSocket,
                        Symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT" },
                        TargetUpdateInterval = TimeSpan.FromSeconds(2)
                    }
                ]
            };
    }

    /// <summary>
    /// Формирует конфигурацию правил алертинга по умолчанию.
    /// </summary>
    /// <returns>Набор конфигураций правил.</returns>
    public static IReadOnlyCollection<AlertRuleConfig> CreateAlertRules()
    {
        return
        [
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["min_price"] = "18000",
                    ["max_price"] = "22000"
                }
            },
            new AlertRuleConfig
            {
                RuleName = "VolumeSpike",
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["min_volume"] = "4",
                    ["min_count"] = "5"
                }
            }
        ];
    }
}

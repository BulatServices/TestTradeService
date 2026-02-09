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
    /// <returns>Конфигурация инструментов.</returns>
    public static MarketInstrumentsConfig CreateInstruments()
    {
        return new MarketInstrumentsConfig
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
        var priceThresholdRules = CreateInstruments()
            .Profiles
            .Where(profile => profile.Transport == MarketDataSourceTransport.WebSocket)
            .SelectMany(profile => profile.Symbols.Select(symbol => new
            {
                Exchange = profile.Exchange.ToString(),
                Symbol = symbol,
                Bounds = ResolvePriceBounds(symbol)
            }))
            .Where(item => item.Bounds is not null)
            .Select(item => new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = item.Exchange,
                Symbol = item.Symbol,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["min_price"] = item.Bounds!.Value.Min.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["max_price"] = item.Bounds!.Value.Max.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            })
            .ToArray();

        return
        [
            ..priceThresholdRules,
            new AlertRuleConfig
            {
                RuleName = "VolumeSpike",
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["min_volume"] = "4",
                    ["min_count"] = "5",
                    ["volume_spike_ratio"] = "2.0"
                }
            },
            new AlertRuleConfig
            {
                RuleName = AlertingChannels.GlobalRuleName,
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AlertingChannels.ChannelsParameterKey] = AlertingChannels.ToCsv(AlertingChannels.Default())
                }
            }
        ];
    }

    private static (decimal Min, decimal Max)? ResolvePriceBounds(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        var upperSymbol = symbol.ToUpperInvariant();
        if (upperSymbol.Contains("BTC", StringComparison.Ordinal) || upperSymbol.Contains("XBT", StringComparison.Ordinal))
            return (10_000m, 200_000m);

        if (upperSymbol.Contains("ETH", StringComparison.Ordinal))
            return (500m, 10_000m);

        if (upperSymbol.Contains("SOL", StringComparison.Ordinal))
            return (10m, 1_000m);

        return null;
    }
}

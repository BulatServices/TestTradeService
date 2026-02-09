using TestTradeService.Models;
using TestTradeService.Storage;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты выбора параметров правила из конфигурации.
/// </summary>
public sealed class AlertRuleConfigProviderTests
{
    /// <summary>
    /// Проверяет приоритет символ-специфичной конфигурации над глобальной.
    /// </summary>
    [Fact]
    public void GetParameters_WhenSymbolSpecificExists_ReturnsMostSpecific()
    {
        var provider = new AlertRuleConfigProvider(new[]
        {
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>
                {
                    ["min_price"] = "100",
                    ["max_price"] = "200"
                }
            },
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = "Kraken",
                Symbol = "XBT/USD",
                Parameters = new Dictionary<string, string>
                {
                    ["min_price"] = "150",
                    ["max_price"] = "250"
                }
            }
        });

        var parameters = provider.GetParameters("PriceThreshold", "Kraken-WebSocket", "XBT/USD");

        Assert.Equal("150", parameters["min_price"]);
        Assert.Equal("250", parameters["max_price"]);
    }

    /// <summary>
    /// Проверяет, что отключенное правило не участвует в выборе параметров.
    /// </summary>
    [Fact]
    public void GetParameters_WhenRuleDisabled_ReturnsEmpty()
    {
        var provider = new AlertRuleConfigProvider(new[]
        {
            new AlertRuleConfig
            {
                RuleName = "VolumeSpike",
                Enabled = false,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>
                {
                    ["min_volume"] = "4",
                    ["min_count"] = "5"
                }
            }
        });

        var parameters = provider.GetParameters("VolumeSpike", "Bybit-Rest", "BTCUSDT");

        Assert.Empty(parameters);
    }

    /// <summary>
    /// Проверяет детерминированный выбор при нескольких правилах одинаковой специфичности.
    /// </summary>
    [Fact]
    public void GetParameters_WhenSpecificityIsEqual_UsesStableTieBreaker()
    {
        var provider = new AlertRuleConfigProvider(new[]
        {
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = "KRAKEN",
                Symbol = "XBT/USD",
                Parameters = new Dictionary<string, string>
                {
                    ["min_price"] = "300"
                }
            },
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = "kraken",
                Symbol = "xbt/usd",
                Parameters = new Dictionary<string, string>
                {
                    ["min_price"] = "100"
                }
            }
        });

        var parameters = provider.GetParameters("PriceThreshold", "Kraken-WebSocket", "XBT/USD");

        Assert.Equal("100", parameters["min_price"]);
    }
}

using TestTradeService.Api.Contracts;
using TestTradeService.Models;
using TestTradeService.Storage;
using TestTradeService.Storage.Repositories;
using Xunit;

namespace TestTradeService.Tests;

/// <summary>
/// Тесты in-memory репозитория правил алертинга.
/// </summary>
public sealed class InMemoryAlertRepositoryTests
{
    /// <summary>
    /// Проверяет, что служебная глобальная конфигурация не попадает в список Items и отдается в GlobalChannels.
    /// </summary>
    [Fact]
    public async Task GetAlertRulesAsync_WhenGlobalConfigExists_ReturnsGlobalChannelsSeparately()
    {
        var provider = new AlertRuleConfigProvider(new[]
        {
            new AlertRuleConfig
            {
                RuleName = "PriceThreshold",
                Enabled = true,
                Exchange = null,
                Symbol = null,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["min_price"] = "100",
                    ["max_price"] = "200"
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
                    [AlertingChannels.ChannelsParameterKey] = "Console,File"
                }
            }
        });
        var repository = new InMemoryAlertRepository(provider);

        var result = await repository.GetAlertRulesAsync(CancellationToken.None);
        var item = Assert.Single(result.Items);

        Assert.Equal("PriceThreshold", item.RuleName);
        Assert.Equal(new[] { "Console", "File" }, result.GlobalChannels);
    }

    /// <summary>
    /// Проверяет, что при сохранении правил репозиторий формирует и сохраняет служебную глобальную конфигурацию каналов.
    /// </summary>
    [Fact]
    public async Task SaveAlertRulesAsync_WhenCalled_StoresGlobalChannelsConfig()
    {
        var provider = new AlertRuleConfigProvider(Array.Empty<AlertRuleConfig>());
        var repository = new InMemoryAlertRepository(provider);

        await repository.SaveAlertRulesAsync(new PutAlertRulesRequest
        {
            Items =
            [
                new AlertRuleConfigDto
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
                }
            ],
            GlobalChannels = new[] { "file", "Console", "File" }
        }, CancellationToken.None);

        var allRules = provider.GetAll();
        var globalConfig = allRules.Single(x => x.RuleName == AlertingChannels.GlobalRuleName);
        Assert.Equal(
            new[] { "Console", "File" },
            AlertingChannels.ParseCsv(globalConfig.Parameters[AlertingChannels.ChannelsParameterKey]).OrderBy(x => x));
    }
}

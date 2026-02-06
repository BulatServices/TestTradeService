using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Core;

namespace TestTradeService.Ingestion.Channels.Binance;

/// <summary>
/// Конечная реализация REST-канала Binance для потока тикера.
/// </summary>
public sealed class BinanceTickerRestChannel : RestPollingChannelBase
{
    private readonly HttpClient _httpClient;
    private readonly RestChannelSettings _settings;

    /// <summary>
    /// Инициализирует REST-канал тикера Binance.
    /// </summary>
    public BinanceTickerRestChannel(
        string id,
        IReadOnlyCollection<string> symbols,
        RestChannelSettings settings,
        HttpClient httpClient,
        ILogger<BinanceTickerRestChannel> logger)
        : base(id, "Binance", symbols, settings, logger)
    {
        _settings = settings;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyCollection<string>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken cancellationToken)
    {
        var messages = new List<string>(symbolsBatch.Count);

        foreach (var symbol in symbolsBatch)
        {
            var endpoint = $"{_settings.Endpoint}?symbol={symbol.ToUpperInvariant()}";
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            messages.Add(payload);
        }

        return messages;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> BuildMetadata(IReadOnlyCollection<string> symbolsBatch)
    {
        return new Dictionary<string, string>
        {
            ["endpoint"] = _settings.Endpoint,
            ["symbols"] = string.Join(',', symbolsBatch)
        };
    }
}

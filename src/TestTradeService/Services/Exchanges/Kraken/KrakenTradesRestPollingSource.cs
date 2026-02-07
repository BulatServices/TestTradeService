using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Kraken;

/// <summary>
/// REST polling-источник сделок Kraken (endpoint <c>/0/public/Trades</c>).
/// </summary>
public sealed class KrakenTradesRestPollingSource : RestPollingSource
{
    private const string TradesEndpoint = "https://api.kraken.com/0/public/Trades";

    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _sinceBySymbol = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Инициализирует REST polling-источник сделок Kraken.
    /// </summary>
    public KrakenTradesRestPollingSource(
        ILogger<KrakenTradesRestPollingSource> logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore,
        IHttpClientFactory httpClientFactory)
        : base(logger, instrumentsConfig, storage, tradeCursorStore, MarketExchange.Kraken)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(KrakenTradesRestPollingSource));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TestTradeService/1.0");
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyCollection<Tick>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken ct)
    {
        var ticks = new List<Tick>();

        foreach (var symbol in symbolsBatch)
        {
            var restPair = ToRestPair(symbol);
            var query = $"?pair={Uri.EscapeDataString(restPair)}";

            if (_sinceBySymbol.TryGetValue(symbol, out var since) && !string.IsNullOrWhiteSpace(since))
                query += $"&since={Uri.EscapeDataString(since)}";

            using var response = await _httpClient.GetAsync(TradesEndpoint + query, ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(ct);
            var parsed = KrakenTradesParsing.ParseRestTrades(symbol, payload, out var last);
            ticks.AddRange(parsed);

            if (!string.IsNullOrWhiteSpace(last))
                _sinceBySymbol[symbol] = last!;
        }

        return ticks;
    }

    private static string ToRestPair(string symbol)
    {
        return symbol.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}

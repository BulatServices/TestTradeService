using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Coinbase;

/// <summary>
/// REST polling-источник сделок Coinbase Exchange (endpoint <c>/products/{id}/trades</c>).
/// </summary>
public sealed class CoinbaseTradesRestPollingSource : RestPollingSource
{
    private const string ApiBase = "https://api.exchange.coinbase.com";

    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, long> _lastTradeIdBySymbol = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Инициализирует REST polling-источник сделок Coinbase Exchange.
    /// </summary>
    public CoinbaseTradesRestPollingSource(
        ILogger<CoinbaseTradesRestPollingSource> logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore,
        IHttpClientFactory httpClientFactory)
        : base(logger, instrumentsConfig, storage, tradeCursorStore, MarketExchange.Coinbase)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(CoinbaseTradesRestPollingSource));
        _httpClient.BaseAddress = new Uri(ApiBase);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TestTradeService/1.0");
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyCollection<Tick>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken ct)
    {
        var result = new List<Tick>();

        foreach (var symbol in symbolsBatch)
        {
            var endpoint = $"/products/{Uri.EscapeDataString(symbol)}/trades?limit=100";
            using var response = await _httpClient.GetAsync(endpoint, ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(ct);
            var ticks = CoinbaseTradesParsing.ParseRestTrades(symbol, payload);

            var lastSeen = _lastTradeIdBySymbol.TryGetValue(symbol, out var last) ? last : 0L;
            var newTicks = ticks
                .Where(t => TryParseTradeId(t.TradeId, out var id) && id > lastSeen)
                .OrderBy(t => t.Timestamp)
                .ToList();

            foreach (var tick in newTicks)
            {
                if (TryParseTradeId(tick.TradeId, out var id) && id > lastSeen)
                    lastSeen = id;

                result.Add(tick);
            }

            _lastTradeIdBySymbol[symbol] = lastSeen;
        }

        return result;
    }

    private static bool TryParseTradeId(string? tradeId, out long id)
    {
        if (string.IsNullOrWhiteSpace(tradeId))
        {
            id = 0;
            return false;
        }

        return long.TryParse(tradeId, out id);
    }
}


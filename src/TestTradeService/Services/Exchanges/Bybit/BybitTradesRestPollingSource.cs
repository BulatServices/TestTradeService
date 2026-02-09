using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services.Exchanges.Bybit;

/// <summary>
/// REST polling-источник сделок Bybit (v5 recent-trade).
/// </summary>
public sealed class BybitTradesRestPollingSource : RestPollingSource
{
    private const string EndpointBase = "https://api.bybit.com/v5/market/recent-trade";

    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, (DateTimeOffset Timestamp, string? TradeId)> _cursorBySymbol = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Инициализирует REST polling-источник сделок Bybit.
    /// </summary>
    public BybitTradesRestPollingSource(
        ILogger<BybitTradesRestPollingSource> logger,
        MarketInstrumentsConfig instrumentsConfig,
        IStorage storage,
        TradeCursorStore tradeCursorStore,
        IHttpClientFactory httpClientFactory)
        : base(logger, instrumentsConfig, storage, tradeCursorStore, MarketExchange.Bybit)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(BybitTradesRestPollingSource));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TestTradeService/1.0");
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyCollection<Tick>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken ct)
    {
        var all = new List<Tick>();

        foreach (var symbol in symbolsBatch)
        {
            var url = $"{EndpointBase}?category=spot&symbol={Uri.EscapeDataString(symbol)}&limit=100";
            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var receivedAt = DateTimeOffset.UtcNow;
            var payload = await response.Content.ReadAsStringAsync(ct);
            var ticks = BybitTradesParsing.ParseRestRecentTrades(symbol, payload)
                .Select(tick => tick with
                {
                    RawExchange = Exchange.ToString(),
                    RawMarketType = MarketType.Spot.ToString(),
                    RawReceivedAt = receivedAt,
                    RawPayload = payload
                })
                .ToList();

            _cursorBySymbol.TryGetValue(symbol, out var cursor);

            var filtered = ticks
                .Where(t =>
                    t.Timestamp > cursor.Timestamp
                    || (t.Timestamp == cursor.Timestamp && !string.IsNullOrWhiteSpace(t.TradeId) && !string.Equals(t.TradeId, cursor.TradeId, StringComparison.Ordinal)))
                .OrderBy(t => t.Timestamp)
                .ToList();

            foreach (var tick in filtered)
            {
                all.Add(tick);
                cursor = (tick.Timestamp, tick.TradeId);
            }

            _cursorBySymbol[symbol] = cursor;
        }

        return all;
    }
}

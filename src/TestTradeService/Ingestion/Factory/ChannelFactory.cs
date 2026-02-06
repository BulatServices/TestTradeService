using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Abstractions;
using TestTradeService.Ingestion.Channels.Binance;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Factory;

/// <summary>
/// Реализация фабрики каналов по матрице Exchange/ChannelKind/StreamType.
/// </summary>
public sealed class ChannelFactory : IChannelFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Инициализирует фабрику каналов.
    /// </summary>
    public ChannelFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IDataChannel Create(ChannelConfig config)
    {
        Validate(config);

        return (config.Exchange, config.ChannelKind, config.StreamType) switch
        {
            ("Binance", ChannelKind.WebSocket, StreamType.Trades) =>
                new BinanceTradesWebSocketChannel(
                    config.Id,
                    config.Symbols,
                    config.WebSocket!,
                    _loggerFactory.CreateLogger<BinanceTradesWebSocketChannel>()),

            ("Binance", ChannelKind.Rest, StreamType.Ticker) =>
                new BinanceTickerRestChannel(
                    config.Id,
                    config.Symbols,
                    config.Rest!,
                    _httpClientFactory.CreateClient(nameof(BinanceTickerRestChannel)),
                    _loggerFactory.CreateLogger<BinanceTickerRestChannel>()),

            _ => throw new NotSupportedException($"Unsupported channel mapping: {config.Exchange}/{config.ChannelKind}/{config.StreamType}")
        };
    }

    private static void Validate(ChannelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("Channel id is required", nameof(config));

        if (string.IsNullOrWhiteSpace(config.Exchange))
            throw new ArgumentException("Exchange is required", nameof(config));

        if (config.Symbols.Count == 0)
            throw new ArgumentException("At least one symbol is required", nameof(config));

        if (config.ChannelKind == ChannelKind.Rest && config.Rest is null)
            throw new ArgumentException("REST settings are required for REST channel", nameof(config));

        if (config.ChannelKind == ChannelKind.WebSocket && config.WebSocket is null)
            throw new ArgumentException("WebSocket settings are required for WS channel", nameof(config));
    }
}

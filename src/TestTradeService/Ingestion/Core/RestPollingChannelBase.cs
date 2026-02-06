using Microsoft.Extensions.Logging;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Ingestion.Models;

namespace TestTradeService.Ingestion.Core;

/// <summary>
/// Базовый класс для REST polling-каналов.
/// </summary>
public abstract class RestPollingChannelBase : BaseChannel
{
    private readonly RestChannelSettings _settings;

    /// <summary>
    /// Инициализирует REST polling-канал.
    /// </summary>
    protected RestPollingChannelBase(string id, string exchange, IReadOnlyCollection<string> symbols, RestChannelSettings settings, ILogger logger)
        : base(id, exchange, ChannelKind.Rest, symbols, logger)
    {
        _settings = settings;
    }

    /// <inheritdoc />
    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var minDelay = TimeSpan.FromSeconds(1d / _settings.RequestsPerSecondLimit);

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var batch in Symbols.Chunk(Math.Max(1, _settings.BatchSize)))
            {
                await PollWithRetryAsync(batch, cancellationToken);
                await Task.Delay(minDelay, cancellationToken);
            }

            await Task.Delay(_settings.PollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Выполняет polling для батча символов и возвращает 0..N сырых payload.
    /// </summary>
    protected abstract Task<IReadOnlyCollection<string>> PollBatchAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken cancellationToken);

    /// <summary>
    /// Строит метаданные сообщения для батча.
    /// </summary>
    protected abstract IReadOnlyDictionary<string, string> BuildMetadata(IReadOnlyCollection<string> symbolsBatch);

    private async Task PollWithRetryAsync(IReadOnlyCollection<string> symbolsBatch, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var backoff = _settings.InitialBackoff;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var payloads = await PollBatchAsync(symbolsBatch, cancellationToken);
                foreach (var payload in payloads)
                {
                    var message = new RawMessage
                    {
                        Exchange = Exchange,
                        ChannelId = Id,
                        TransportType = TransportType.Rest,
                        ReceivedAt = DateTimeOffset.UtcNow,
                        Payload = payload,
                        Metadata = BuildMetadata(symbolsBatch)
                    };

                    await PublishRawMessageAsync(message);
                }

                return;
            }
            catch (Exception ex) when (attempt < _settings.MaxRetries)
            {
                attempt++;
                await PublishErrorAsync(ex);
                await Task.Delay(backoff, cancellationToken);
                backoff = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * 2);
            }
        }
    }
}

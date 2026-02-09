using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

internal sealed class BufferedStorageWriter : IAsyncDisposable
{
    private readonly IStorage _storage;
    private readonly ILogger _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly Channel<StorageEnvelope> _channel;
    private readonly Task _workerTask;

    public BufferedStorageWriter(IStorage storage, PipelinePerformanceOptions options, ILogger logger)
    {
        _storage = storage;
        _logger = logger;
        _batchSize = Math.Max(1, options.BatchSize);
        _flushInterval = ClampFlushInterval(options.FlushInterval);

        var capacity = Math.Max(_batchSize, _batchSize * Math.Max(1, options.MaxInMemoryBatches) * 3);
        _channel = Channel.CreateBounded<StorageEnvelope>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _workerTask = Task.Run(ProcessAsync);
    }

    public ValueTask StoreRawTickAsync(RawTick rawTick, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(new StorageEnvelope(StorageEnvelopeType.RawTick, rawTick), cancellationToken);
    }

    public ValueTask StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(new StorageEnvelope(StorageEnvelopeType.Tick, tick), cancellationToken);
    }

    public ValueTask StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(new StorageEnvelope(StorageEnvelopeType.Aggregate, candle), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _workerTask;
    }

    private async Task ProcessAsync()
    {
        var rawTicks = new List<RawTick>(_batchSize);
        var ticks = new List<NormalizedTick>(_batchSize);
        var aggregates = new List<AggregatedCandle>(_batchSize);

        while (true)
        {
            while (_channel.Reader.TryRead(out var envelope))
            {
                AddEnvelope(envelope, rawTicks, ticks, aggregates);
                if (ReachedBatchThreshold(rawTicks, ticks, aggregates))
                {
                    await FlushAsync(rawTicks, ticks, aggregates);
                }
            }

            var waitToReadTask = _channel.Reader.WaitToReadAsync().AsTask();
            var delayTask = Task.Delay(_flushInterval);
            var completed = await Task.WhenAny(waitToReadTask, delayTask);

            if (completed == delayTask)
            {
                await FlushAsync(rawTicks, ticks, aggregates);
                continue;
            }

            if (!await waitToReadTask)
            {
                break;
            }
        }

        await FlushAsync(rawTicks, ticks, aggregates);
        _logger.LogInformation("Buffered storage writer stopped");
    }

    private static void AddEnvelope(
        StorageEnvelope envelope,
        List<RawTick> rawTicks,
        List<NormalizedTick> ticks,
        List<AggregatedCandle> aggregates)
    {
        switch (envelope.Type)
        {
            case StorageEnvelopeType.RawTick:
                rawTicks.Add((RawTick)envelope.Payload);
                break;
            case StorageEnvelopeType.Tick:
                ticks.Add((NormalizedTick)envelope.Payload);
                break;
            case StorageEnvelopeType.Aggregate:
                aggregates.Add((AggregatedCandle)envelope.Payload);
                break;
        }
    }

    private bool ReachedBatchThreshold(
        List<RawTick> rawTicks,
        List<NormalizedTick> ticks,
        List<AggregatedCandle> aggregates)
    {
        return rawTicks.Count >= _batchSize || ticks.Count >= _batchSize || aggregates.Count >= _batchSize;
    }

    private async Task FlushAsync(
        List<RawTick> rawTicks,
        List<NormalizedTick> ticks,
        List<AggregatedCandle> aggregates)
    {
        if (rawTicks.Count == 0 && ticks.Count == 0 && aggregates.Count == 0)
        {
            return;
        }

        if (rawTicks.Count > 0)
        {
            await _storage.StoreRawTicksAsync(rawTicks.ToArray(), CancellationToken.None);
            rawTicks.Clear();
        }

        if (ticks.Count > 0)
        {
            await _storage.StoreTicksAsync(ticks.ToArray(), CancellationToken.None);
            ticks.Clear();
        }

        if (aggregates.Count > 0)
        {
            await _storage.StoreAggregatesAsync(aggregates.ToArray(), CancellationToken.None);
            aggregates.Clear();
        }
    }

    private static TimeSpan ClampFlushInterval(TimeSpan flushInterval)
    {
        if (flushInterval <= TimeSpan.Zero)
        {
            return TimeSpan.FromMilliseconds(250);
        }

        return flushInterval > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : flushInterval;
    }

    private readonly record struct StorageEnvelope(StorageEnvelopeType Type, object Payload);

    private enum StorageEnvelopeType
    {
        RawTick,
        Tick,
        Aggregate
    }
}

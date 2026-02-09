namespace TestTradeService.Services;

/// <summary>
/// Настройки производительности конвейера обработки тиков.
/// </summary>
public sealed class PipelinePerformanceOptions
{
    /// <summary>
    /// Возвращает или задает число партиций обработки по символу.
    /// </summary>
    public int PartitionCount { get; init; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// Возвращает или задает размер одного батча записи в хранилище.
    /// </summary>
    public int BatchSize { get; init; } = 256;

    /// <summary>
    /// Возвращает или задает интервал принудительного flush буфера записи.
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Возвращает или задает максимальное число батчей в памяти до backpressure.
    /// </summary>
    public int MaxInMemoryBatches { get; init; } = 16;

    /// <summary>
    /// Возвращает или задает максимально допустимую конкурентность алертинга.
    /// </summary>
    public int AlertingConcurrency { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);
}

using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;

namespace TestTradeService.Storage.Repositories;

/// <summary>
/// In-memory заглушка чтения обработанных данных при отключенной БД.
/// </summary>
public sealed class InMemoryProcessedReadRepository : IProcessedReadRepository
{
    /// <summary>
    /// Возвращает пустой набор свечей.
    /// </summary>
    /// <param name="request">Параметры фильтрации и пагинации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ со свечами.</returns>
    public Task<CandlesResponseDto> GetCandlesAsync(ProcessedCandlesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CandlesResponseDto
        {
            Total = 0,
            Items = Array.Empty<CandleDto>()
        });
    }

    /// <summary>
    /// Возвращает пустой набор метрик.
    /// </summary>
    /// <param name="request">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ с метриками.</returns>
    public Task<MetricsResponseDto> GetMetricsAsync(ProcessedMetricsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MetricsResponseDto
        {
            Items = Array.Empty<MetricsSnapshotDto>()
        });
    }
}

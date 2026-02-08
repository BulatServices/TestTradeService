using TestTradeService.Api.Contracts;

namespace TestTradeService.Interfaces;

/// <summary>
/// Репозиторий чтения обработанных данных для API.
/// </summary>
public interface IProcessedReadRepository
{
    /// <summary>
    /// Возвращает страницу свечей по фильтрам.
    /// </summary>
    /// <param name="request">Параметры фильтрации и пагинации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пагинированный ответ со свечами.</returns>
    Task<CandlesResponseDto> GetCandlesAsync(ProcessedCandlesQuery request, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает срез метрик по фильтрам.
    /// </summary>
    /// <param name="request">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с метриками.</returns>
    Task<MetricsResponseDto> GetMetricsAsync(ProcessedMetricsQuery request, CancellationToken cancellationToken);
}

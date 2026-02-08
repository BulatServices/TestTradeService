using Microsoft.AspNetCore.Mvc;
using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;

namespace TestTradeService.Api.Controllers;

/// <summary>
/// API чтения обработанных данных.
/// </summary>
[ApiController]
[Route("api/v1/processed")]
public sealed class ProcessedController : ControllerBase
{
    private readonly IProcessedReadRepository _repository;

    /// <summary>
    /// Инициализирует контроллер обработанных данных.
    /// </summary>
    /// <param name="repository">Репозиторий чтения processed-данных.</param>
    public ProcessedController(IProcessedReadRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Возвращает пагинированный список свечей.
    /// </summary>
    /// <param name="query">Параметры фильтрации и пагинации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пагинированный ответ со свечами.</returns>
    [HttpGet("candles")]
    public Task<CandlesResponseDto> GetCandlesAsync([FromQuery] ProcessedCandlesQuery query, CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateRange(query.DateFrom, query.DateTo);
        return _repository.GetCandlesAsync(query, cancellationToken);
    }

    /// <summary>
    /// Возвращает список метрик по фильтрам.
    /// </summary>
    /// <param name="query">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с метриками.</returns>
    [HttpGet("metrics")]
    public Task<MetricsResponseDto> GetMetricsAsync([FromQuery] ProcessedMetricsQuery query, CancellationToken cancellationToken)
    {
        ValidateRange(query.DateFrom, query.DateTo);
        return _repository.GetMetricsAsync(query, cancellationToken);
    }

    private static void ValidateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            throw new ArgumentException("Дата начала диапазона не может быть больше даты окончания.");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentException("Параметр page должен быть не меньше 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentException("Параметр pageSize должен быть в диапазоне от 1 до 100.");
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;

namespace TestTradeService.Api.Controllers;

/// <summary>
/// API управления конфигурацией источников.
/// </summary>
[ApiController]
[Route("api/v1/config/sources")]
public sealed class SourceConfigController : ControllerBase
{
    private readonly ISourceConfigService _sourceConfigService;

    /// <summary>
    /// Инициализирует контроллер конфигурации источников.
    /// </summary>
    /// <param name="sourceConfigService">Сервис конфигурации источников.</param>
    public SourceConfigController(ISourceConfigService sourceConfigService)
    {
        _sourceConfigService = sourceConfigService;
    }

    /// <summary>
    /// Возвращает текущую конфигурацию источников.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Конфигурация источников.</returns>
    [HttpGet]
    public Task<SourceConfigDto> GetAsync(CancellationToken cancellationToken)
    {
        return _sourceConfigService.GetAsync(cancellationToken);
    }

    /// <summary>
    /// Сохраняет и применяет конфигурацию источников.
    /// </summary>
    /// <param name="request">Новая конфигурация источников.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сохраненная конфигурация.</returns>
    [HttpPut]
    public Task<SourceConfigDto> PutAsync([FromBody] SourceConfigDto request, CancellationToken cancellationToken)
    {
        return _sourceConfigService.SaveAsync(request, cancellationToken);
    }
}

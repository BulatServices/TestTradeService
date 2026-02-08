using TestTradeService.Api.Contracts;

namespace TestTradeService.Interfaces;

/// <summary>
/// Сервис чтения, валидации и сохранения конфигурации источников.
/// </summary>
public interface ISourceConfigService
{
    /// <summary>
    /// Возвращает текущую конфигурацию источников для API.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Конфигурация источников.</returns>
    Task<SourceConfigDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет конфигурацию источников в хранилище.
    /// </summary>
    /// <param name="request">Новая конфигурация.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сохраненная конфигурация.</returns>
    Task<SourceConfigDto> SaveAsync(SourceConfigDto request, CancellationToken cancellationToken);
}

namespace TestTradeService.Interfaces;

/// <summary>
/// Выполняет горячее применение конфигурации источников данных в рантайме.
/// </summary>
public interface IRuntimeReconfigurationService
{
    /// <summary>
    /// Применяет обновленную конфигурацию источников без перезапуска процесса.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача применения конфигурации.</returns>
    Task ApplySourcesAsync(CancellationToken cancellationToken);
}

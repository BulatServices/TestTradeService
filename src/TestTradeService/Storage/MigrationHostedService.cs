using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestTradeService.Storage.Configuration;

namespace TestTradeService.Storage;

/// <summary>
/// Hosted-сервис, применяющий SQL-миграции при старте.
/// </summary>
public sealed class MigrationHostedService : IHostedService
{
    private readonly SqlMigrationRunner _runner;
    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<MigrationHostedService> _logger;

    /// <summary>
    /// Инициализирует hosted-сервис миграций.
    /// </summary>
    /// <param name="runner">Раннер миграций.</param>
    /// <param name="databaseOptions">Параметры БД.</param>
    /// <param name="logger">Логгер.</param>
    public MigrationHostedService(
        SqlMigrationRunner runner,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<MigrationHostedService> logger)
    {
        _runner = runner;
        _databaseOptions = databaseOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Применяет миграции при запуске хоста.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача старта hosted-сервиса.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_databaseOptions.AutoMigrate)
            return;

        _logger.LogInformation("Applying SQL migrations...");
        await _runner.RunAsync(cancellationToken);
    }

    /// <summary>
    /// Останавливает hosted-сервис.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача остановки hosted-сервиса.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

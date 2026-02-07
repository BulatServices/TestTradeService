namespace TestTradeService.Monitoring.Configuration;

/// <summary>
/// Конфигурация SLA для мониторинга.
/// </summary>
public sealed class MonitoringSlaConfig
{
    /// <summary>
    /// Максимально допустимая задержка обработки тиков.
    /// </summary>
    public TimeSpan MaxTickDelay { get; init; } = TimeSpan.FromSeconds(2);
}

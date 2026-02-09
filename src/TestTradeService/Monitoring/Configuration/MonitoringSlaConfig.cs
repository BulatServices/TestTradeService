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

    /// <summary>
    /// Множитель SLA для предупредительного статуса.
    /// </summary>
    public double WarnMultiplier { get; init; } = 2d;

    /// <summary>
    /// Множитель SLA для критического статуса.
    /// </summary>
    public double CriticalMultiplier { get; init; } = 4d;

    /// <summary>
    /// Размер rolling-окна для отчета производительности.
    /// </summary>
    public TimeSpan RollingWindow { get; init; } = TimeSpan.FromMinutes(5);
}

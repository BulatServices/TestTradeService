using Microsoft.AspNetCore.Mvc;
using TestTradeService.Api.Contracts;
using TestTradeService.Interfaces;

namespace TestTradeService.Api.Controllers;

/// <summary>
/// API мониторинга и алертинга.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class MonitoringController : ControllerBase
{
    private readonly IMonitoringService _monitoringService;
    private readonly IAlertReadRepository _alertReadRepository;
    private readonly IAlertRuleWriteRepository _alertRuleWriteRepository;

    /// <summary>
    /// Инициализирует контроллер мониторинга.
    /// </summary>
    /// <param name="monitoringService">Сервис мониторинга.</param>
    /// <param name="alertReadRepository">Репозиторий чтения алертов.</param>
    /// <param name="alertRuleWriteRepository">Репозиторий записи правил алертинга.</param>
    public MonitoringController(
        IMonitoringService monitoringService,
        IAlertReadRepository alertReadRepository,
        IAlertRuleWriteRepository alertRuleWriteRepository)
    {
        _monitoringService = monitoringService;
        _alertReadRepository = alertReadRepository;
        _alertRuleWriteRepository = alertRuleWriteRepository;
    }

    /// <summary>
    /// Возвращает снимок мониторинга системы.
    /// </summary>
    /// <returns>Снимок мониторинга.</returns>
    [HttpGet("monitoring/snapshot")]
    public MonitoringSnapshotDto GetSnapshot()
    {
        var snapshot = _monitoringService.Snapshot();
        return new MonitoringSnapshotDto
        {
            Timestamp = snapshot.Timestamp,
            ExchangeStats = snapshot.ExchangeStats.ToDictionary(
                x => x.Key.ToString(),
                x => new ExchangeStatsDto
                {
                    Exchange = x.Key.ToString(),
                    TickCount = x.Value.TickCount,
                    AggregateCount = x.Value.AggregateCount,
                    AverageDelayMs = x.Value.AverageDelayMs,
                    LastTickTime = x.Value.LastTickTime,
                    WindowTickCount = x.Value.WindowTickCount,
                    WindowAggregateCount = x.Value.WindowAggregateCount,
                    WindowAvgDelayMs = x.Value.WindowAvgDelayMs,
                    WindowMaxDelayMs = x.Value.WindowMaxDelayMs,
                    WindowTickRatePerSec = x.Value.WindowTickRatePerSec,
                    WindowAggregateRatePerSec = x.Value.WindowAggregateRatePerSec
                }),
            SourceStats = snapshot.SourceStats.ToDictionary(
                x => x.Key,
                x => new SourceStatsDto
                {
                    Source = x.Value.Source,
                    TickCount = x.Value.TickCount,
                    AggregateCount = x.Value.AggregateCount,
                    AverageDelayMs = x.Value.AverageDelayMs,
                    LastTickTime = x.Value.LastTickTime,
                    Status = x.Value.Status.ToString(),
                    LastTickAgeMs = x.Value.LastTickAgeMs,
                    WindowTickCount = x.Value.WindowTickCount,
                    WindowAggregateCount = x.Value.WindowAggregateCount,
                    WindowAvgDelayMs = x.Value.WindowAvgDelayMs,
                    WindowMaxDelayMs = x.Value.WindowMaxDelayMs,
                    WindowTickRatePerSec = x.Value.WindowTickRatePerSec,
                    WindowAggregateRatePerSec = x.Value.WindowAggregateRatePerSec
                }),
            PerformanceReport = new PerformanceReportDto
            {
                WindowMinutes = snapshot.PerformanceReport.WindowMinutes,
                TotalWindowTickCount = snapshot.PerformanceReport.TotalWindowTickCount,
                TotalWindowAggregateCount = snapshot.PerformanceReport.TotalWindowAggregateCount,
                TotalWindowAvgDelayMs = snapshot.PerformanceReport.TotalWindowAvgDelayMs,
                TotalWindowMaxDelayMs = snapshot.PerformanceReport.TotalWindowMaxDelayMs,
                TotalWindowTickRatePerSec = snapshot.PerformanceReport.TotalWindowTickRatePerSec,
                TotalWindowAggregateRatePerSec = snapshot.PerformanceReport.TotalWindowAggregateRatePerSec,
                SourcesOk = snapshot.PerformanceReport.SourcesOk,
                SourcesWarn = snapshot.PerformanceReport.SourcesWarn,
                SourcesCritical = snapshot.PerformanceReport.SourcesCritical
            },
            Warnings = snapshot.Warnings
        };
    }

    /// <summary>
    /// Возвращает ленту алертов по фильтрам.
    /// </summary>
    /// <param name="query">Параметры фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с алертами.</returns>
    [HttpGet("alerts")]
    public Task<AlertsResponseDto> GetAlertsAsync([FromQuery] AlertsQuery query, CancellationToken cancellationToken)
    {
        if (query.DateFrom.HasValue && query.DateTo.HasValue && query.DateFrom > query.DateTo)
        {
            throw new ArgumentException("Дата начала диапазона не может быть больше даты окончания.");
        }

        return _alertReadRepository.GetAlertsAsync(query, cancellationToken);
    }

    /// <summary>
    /// Возвращает набор правил алертинга.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ с правилами алертинга.</returns>
    [HttpGet("alerts/rules")]
    public Task<AlertRulesResponseDto> GetAlertRulesAsync(CancellationToken cancellationToken)
    {
        return _alertReadRepository.GetAlertRulesAsync(cancellationToken);
    }

    /// <summary>
    /// Сохраняет набор правил алертинга.
    /// </summary>
    /// <param name="request">Новый снимок правил.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сохраненный набор правил алертинга.</returns>
    [HttpPut("alerts/rules")]
    public async Task<AlertRulesResponseDto> PutAlertRulesAsync([FromBody] PutAlertRulesRequest request, CancellationToken cancellationToken)
    {
        await _alertRuleWriteRepository.SaveAlertRulesAsync(request, cancellationToken);
        return await _alertReadRepository.GetAlertRulesAsync(cancellationToken);
    }
}

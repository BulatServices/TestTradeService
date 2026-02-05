using System.Text;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Канал уведомлений в файл.
/// </summary>
public sealed class FileNotifier : INotifier
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "alerts.log");

    /// <summary>
    /// Имя канала уведомления.
    /// </summary>
    public string Name => "File";

    /// <summary>
    /// Записывает алерт в лог-файл.
    /// </summary>
    public async Task NotifyAsync(Alert alert, CancellationToken cancellationToken)
    {
        var line = $"{alert.Timestamp:O} [{alert.Rule}] {alert.Symbol} {alert.Message}{Environment.NewLine}";
        await File.AppendAllTextAsync(_path, line, Encoding.UTF8, cancellationToken);
    }
}

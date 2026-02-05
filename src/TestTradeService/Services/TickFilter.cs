using TestTradeService.Models;

namespace TestTradeService.Services;

/// <summary>
/// Выполняет фильтрацию тиков по списку интересующих инструментов.
/// </summary>
public sealed class TickFilter
{
    private readonly HashSet<string> _allowedSymbols;

    /// <summary>
    /// Создает фильтр по набору разрешенных символов.
    /// </summary>
    /// <param name="allowedSymbols">Список символов для обработки.</param>
    public TickFilter(IEnumerable<string> allowedSymbols)
    {
        _allowedSymbols = new HashSet<string>(allowedSymbols, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, должен ли тик попасть в обработку.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <returns><c>true</c>, если символ разрешен.</returns>
    public bool IsAllowed(NormalizedTick tick) => _allowedSymbols.Contains(tick.Symbol);
}

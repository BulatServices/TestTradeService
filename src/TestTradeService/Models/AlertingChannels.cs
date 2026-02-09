namespace TestTradeService.Models;

/// <summary>
/// Вспомогательные константы и методы для настройки каналов алертинга.
/// </summary>
public static class AlertingChannels
{
    /// <summary>
    /// Служебное имя правила для глобальной конфигурации алертинга.
    /// </summary>
    public const string GlobalRuleName = "__AlertingGlobal__";

    /// <summary>
    /// Имя параметра с CSV-списком каналов уведомлений.
    /// </summary>
    public const string ChannelsParameterKey = "channels";

    /// <summary>
    /// Канал отправки уведомлений в консоль.
    /// </summary>
    public const string Console = "Console";

    /// <summary>
    /// Канал отправки уведомлений в файл.
    /// </summary>
    public const string File = "File";

    /// <summary>
    /// Канал отправки уведомлений в email-заглушку.
    /// </summary>
    public const string EmailStub = "EmailStub";

    /// <summary>
    /// Возвращает стандартный набор каналов уведомлений.
    /// </summary>
    /// <returns>Список стандартных каналов.</returns>
    public static IReadOnlyCollection<string> Default()
    {
        return
        [
            Console,
            File,
            EmailStub
        ];
    }

    /// <summary>
    /// Нормализует и очищает список каналов уведомлений.
    /// </summary>
    /// <param name="channels">Исходный список каналов.</param>
    /// <returns>Нормализованный список без дубликатов.</returns>
    public static IReadOnlyCollection<string> Normalize(IEnumerable<string> channels)
    {
        var known = KnownByLower();
        return channels
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => known.GetValueOrDefault(value!.ToLowerInvariant(), value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Преобразует CSV-строку каналов в список.
    /// </summary>
    /// <param name="csv">CSV-строка каналов.</param>
    /// <returns>Нормализованный список каналов.</returns>
    public static IReadOnlyCollection<string> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return Normalize(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Преобразует список каналов в CSV-строку.
    /// </summary>
    /// <param name="channels">Список каналов.</param>
    /// <returns>CSV-строка каналов.</returns>
    public static string ToCsv(IEnumerable<string> channels)
    {
        return string.Join(',', Normalize(channels));
    }

    /// <summary>
    /// Проверяет, поддерживается ли имя канала уведомлений.
    /// </summary>
    /// <param name="channel">Имя канала.</param>
    /// <returns><c>true</c>, если канал поддерживается.</returns>
    public static bool IsKnown(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        return KnownByLower().ContainsKey(channel.Trim().ToLowerInvariant());
    }

    private static Dictionary<string, string> KnownByLower()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Console.ToLowerInvariant()] = Console,
            [File.ToLowerInvariant()] = File,
            [EmailStub.ToLowerInvariant()] = EmailStub
        };
    }
}

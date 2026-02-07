using TestTradeService.Models;

namespace TestTradeService.Ingestion.Configuration;

/// <summary>
/// Описание набора инструментов для конкретного рынка.
/// </summary>
public sealed class MarketInstrumentProfile
{
    /// <summary>
    /// Тип рынка, к которому относится набор.
    /// </summary>
    public required MarketType MarketType { get; init; }

    /// <summary>
    /// Минимальный набор символов для подписки/опроса.
    /// </summary>
    public required IReadOnlyCollection<string> Symbols { get; init; }

    /// <summary>
    /// Целевая частота обновления (poll interval или частота тиков).
    /// </summary>
    public TimeSpan TargetUpdateInterval { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Конфигурация наборов инструментов по рынкам.
/// </summary>
public sealed class MarketInstrumentsConfig
{
    /// <summary>
    /// Наборы инструментов по рынкам.
    /// </summary>
    public IReadOnlyCollection<MarketInstrumentProfile> Profiles { get; init; } = Array.Empty<MarketInstrumentProfile>();

    /// <summary>
    /// Возвращает объединенный список всех символов.
    /// </summary>
    /// <returns>Список символов без повторов.</returns>
    public IReadOnlyCollection<string> GetAllSymbols()
    {
        return Profiles
            .SelectMany(profile => profile.Symbols)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Возвращает профиль инструментов по типу рынка.
    /// </summary>
    /// <param name="marketType">Тип рынка.</param>
    /// <returns>Профиль инструментов или <c>null</c>, если профиль отсутствует.</returns>
    public MarketInstrumentProfile? GetProfile(MarketType marketType)
    {
        return Profiles.FirstOrDefault(profile => profile.MarketType == marketType);
    }
}

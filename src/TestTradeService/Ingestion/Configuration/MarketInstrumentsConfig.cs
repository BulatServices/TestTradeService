using TestTradeService.Models;

namespace TestTradeService.Ingestion.Configuration;

/// <summary>
/// Описание набора инструментов для конкретного рынка.
/// </summary>
public sealed class MarketInstrumentProfile
{
    /// <summary>
    /// Биржа, к которой относится набор инструментов.
    /// </summary>
    public required MarketExchange Exchange { get; init; }

    /// <summary>
    /// Тип рынка, к которому относится набор.
    /// </summary>
    public required MarketType MarketType { get; init; }

    /// <summary>
    /// Транспорт источника данных.
    /// </summary>
    public required MarketDataSourceTransport Transport { get; init; }

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
    private IReadOnlyCollection<MarketInstrumentProfile> _profiles = Array.Empty<MarketInstrumentProfile>();

    /// <summary>
    /// Наборы инструментов по рынкам.
    /// </summary>
    public IReadOnlyCollection<MarketInstrumentProfile> Profiles
    {
        get => _profiles;
        init => _profiles = value;
    }

    /// <summary>
    /// Возвращает объединенный список всех символов.
    /// </summary>
    /// <returns>Список символов без повторов.</returns>
    public IReadOnlyCollection<string> GetAllSymbols()
    {
        return _profiles
            .SelectMany(profile => profile.Symbols)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Возвращает профиль инструментов по типу рынка.
    /// </summary>
    /// <param name="exchange">Биржа.</param>
    /// <param name="marketType">Тип рынка.</param>
    /// <returns>Профиль инструментов или <c>null</c>, если профиль отсутствует.</returns>
    public MarketInstrumentProfile? GetProfile(MarketExchange exchange, MarketType marketType)
    {
        return _profiles.FirstOrDefault(profile => profile.Exchange == exchange && profile.MarketType == marketType);
    }

    /// <summary>
    /// Возвращает профиль инструментов по типу рынка и транспорту.
    /// </summary>
    /// <param name="exchange">Биржа.</param>
    /// <param name="marketType">Тип рынка.</param>
    /// <param name="transport">Транспорт источника.</param>
    /// <returns>Профиль инструментов или <c>null</c>, если профиль отсутствует.</returns>
    public MarketInstrumentProfile? GetProfile(MarketExchange exchange, MarketType marketType, MarketDataSourceTransport transport)
    {
        return _profiles.FirstOrDefault(profile =>
            profile.Exchange == exchange
            && profile.MarketType == marketType
            && profile.Transport == transport);
    }

    /// <summary>
    /// Заменяет текущий снимок профилей инструментов.
    /// </summary>
    /// <param name="profiles">Новый набор профилей.</param>
    public void ReplaceProfiles(IReadOnlyCollection<MarketInstrumentProfile> profiles)
    {
        _profiles = profiles;
    }
}

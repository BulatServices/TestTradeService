using Dapper;
using TestTradeService.Api.Contracts;
using TestTradeService.Ingestion.Configuration;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage.Services;

/// <summary>
/// Сервис чтения, валидации и сохранения конфигурации источников.
/// </summary>
public sealed class SourceConfigService : ISourceConfigService
{
    private readonly MarketInstrumentsConfig _instrumentsConfig;
    private readonly IRuntimeReconfigurationService _runtimeReconfigurationService;
    private readonly MetadataDataSource? _metadataDataSource;

    /// <summary>
    /// Инициализирует сервис конфигурации источников.
    /// </summary>
    /// <param name="instrumentsConfig">Текущая runtime-конфигурация инструментов.</param>
    /// <param name="runtimeReconfigurationService">Сервис hot reload источников.</param>
    /// <param name="metadataDataSource">Источник подключений к БД метаданных.</param>
    public SourceConfigService(
        MarketInstrumentsConfig instrumentsConfig,
        IRuntimeReconfigurationService runtimeReconfigurationService,
        MetadataDataSource? metadataDataSource = null)
    {
        _instrumentsConfig = instrumentsConfig;
        _runtimeReconfigurationService = runtimeReconfigurationService;
        _metadataDataSource = metadataDataSource;
    }

    /// <summary>
    /// Возвращает текущую конфигурацию источников для API.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Конфигурация источников.</returns>
    public Task<SourceConfigDto> GetAsync(CancellationToken cancellationToken)
    {
        return GetInternalAsync(cancellationToken);
    }

    /// <summary>
    /// Сохраняет конфигурацию источников в хранилище.
    /// </summary>
    /// <param name="request">Новая конфигурация.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сохраненная конфигурация.</returns>
    public async Task<SourceConfigDto> SaveAsync(SourceConfigDto request, CancellationToken cancellationToken)
    {
        Validate(request);
        var profiles = request.Profiles
            .Where(x => x.IsEnabled)
            .Select(ToProfile)
            .ToArray();

        if (_metadataDataSource is not null)
        {
            await PersistToDatabaseAsync(profiles, cancellationToken);
            profiles = await LoadProfilesFromDatabaseAsync(cancellationToken);
        }

        _instrumentsConfig.ReplaceProfiles(profiles);
        await _runtimeReconfigurationService.ApplySourcesAsync(cancellationToken);

        return ToDto(profiles);
    }

    private async Task<SourceConfigDto> GetInternalAsync(CancellationToken cancellationToken)
    {
        var profiles = _metadataDataSource is null
            ? _instrumentsConfig.Profiles.ToArray()
            : await LoadProfilesFromDatabaseAsync(cancellationToken);

        _instrumentsConfig.ReplaceProfiles(profiles);
        return ToDto(profiles);
    }

    private static SourceConfigDto ToDto(IReadOnlyCollection<MarketInstrumentProfile> profiles)
    {
        return new SourceConfigDto
        {
            Profiles = profiles
                .Select(profile => new SourceProfileDto
                {
                    Exchange = profile.Exchange.ToString(),
                    MarketType = profile.MarketType.ToString(),
                    Transport = profile.Transport.ToString(),
                    Symbols = profile.Symbols.ToArray(),
                    TargetUpdateIntervalMs = (int)Math.Max(1, profile.TargetUpdateInterval.TotalMilliseconds),
                    IsEnabled = true
                })
                .OrderBy(x => x.Exchange, StringComparer.Ordinal)
                .ThenBy(x => x.MarketType, StringComparer.Ordinal)
                .ThenBy(x => x.Transport, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private async Task<MarketInstrumentProfile[]> LoadProfilesFromDatabaseAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            select
                exchange,
                market_type as MarketType,
                transport as Transport,
                symbol,
                target_update_interval_ms as TargetUpdateIntervalMs
            from meta.instruments
            where is_active = true
            """;

        await using var connection = await _metadataDataSource!.DataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<InstrumentConfigRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();

        return rows
            .GroupBy(r => $"{r.Exchange}::{r.MarketType}::{r.Transport}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                if (!Enum.TryParse<MarketExchange>(first.Exchange, true, out var exchange))
                {
                    return null;
                }

                return new MarketInstrumentProfile
                {
                    Exchange = exchange,
                    MarketType = Enum.Parse<MarketType>(first.MarketType, true),
                    Transport = Enum.Parse<MarketDataSourceTransport>(first.Transport, true),
                    Symbols = group
                        .Select(x => x.Symbol)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    TargetUpdateInterval = TimeSpan.FromMilliseconds(group.Max(x => x.TargetUpdateIntervalMs))
                };
            })
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .ToArray();
    }

    private async Task PersistToDatabaseAsync(IReadOnlyCollection<MarketInstrumentProfile> profiles, CancellationToken cancellationToken)
    {
        await using var connection = await _metadataDataSource!.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string disableSql = "update meta.instruments set is_active = false, updated_at = now();";
        await connection.ExecuteAsync(new CommandDefinition(disableSql, transaction: transaction, cancellationToken: cancellationToken));

        const string upsertSql = """
            insert into meta.instruments
            (
                exchange,
                market_type,
                transport,
                symbol,
                base_asset,
                quote_asset,
                description,
                price_tick_size,
                volume_step,
                price_decimals,
                volume_decimals,
                contract_size,
                min_notional,
                target_update_interval_ms,
                is_active,
                updated_at
            )
            values
            (
                @Exchange,
                @MarketType,
                @Transport,
                @Symbol,
                @BaseAsset,
                @QuoteAsset,
                @Description,
                0.01,
                0.0001,
                2,
                4,
                @ContractSize,
                10,
                @TargetUpdateIntervalMs,
                true,
                now()
            )
            on conflict (exchange, market_type, symbol) do update
            set
                transport = excluded.transport,
                target_update_interval_ms = excluded.target_update_interval_ms,
                is_active = true,
                updated_at = now();
            """;

        foreach (var profile in profiles)
        {
            foreach (var symbol in profile.Symbols)
            {
                var (baseAsset, quoteAsset) = ParseSymbol(symbol);
                var isPerp = profile.MarketType == MarketType.Perp;
                await connection.ExecuteAsync(new CommandDefinition(upsertSql, new
                {
                    Exchange = profile.Exchange.ToString(),
                    MarketType = profile.MarketType.ToString(),
                    Transport = profile.Transport.ToString(),
                    Symbol = symbol,
                    BaseAsset = baseAsset,
                    QuoteAsset = quoteAsset,
                    Description = $"{symbol} {profile.MarketType}",
                    ContractSize = isPerp ? 1m : (decimal?)null,
                    TargetUpdateIntervalMs = (int)Math.Max(1, profile.TargetUpdateInterval.TotalMilliseconds)
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static MarketInstrumentProfile ToProfile(SourceProfileDto profile)
    {
        return new MarketInstrumentProfile
        {
            Exchange = Enum.Parse<MarketExchange>(profile.Exchange, true),
            MarketType = Enum.Parse<MarketType>(profile.MarketType, true),
            Transport = Enum.Parse<MarketDataSourceTransport>(profile.Transport, true),
            Symbols = profile.Symbols
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TargetUpdateInterval = TimeSpan.FromMilliseconds(profile.TargetUpdateIntervalMs)
        };
    }

    private static (string BaseAsset, string QuoteAsset) ParseSymbol(string symbol)
    {
        var parts = symbol
            .Split(new[] { '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            2 => (parts[0], parts[1]),
            _ => (symbol, "UNKNOWN")
        };
    }

    private static void Validate(SourceConfigDto request)
    {
        foreach (var profile in request.Profiles)
        {
            if (profile.TargetUpdateIntervalMs <= 0)
            {
                throw new ArgumentException("Интервал обновления должен быть больше нуля.", nameof(request));
            }

            if (profile.Symbols.Count == 0)
            {
                throw new ArgumentException("Для профиля должен быть указан хотя бы один символ.", nameof(request));
            }

            _ = Enum.Parse<MarketExchange>(profile.Exchange, true);
            _ = Enum.Parse<MarketType>(profile.MarketType, true);
            _ = Enum.Parse<MarketDataSourceTransport>(profile.Transport, true);
        }
    }

    private sealed record InstrumentConfigRow
    {
        public required string Exchange { get; init; }
        public required string MarketType { get; init; }
        public required string Transport { get; init; }
        public required string Symbol { get; init; }
        public required int TargetUpdateIntervalMs { get; init; }
    }
}

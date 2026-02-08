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
        var dto = new SourceConfigDto
        {
            Profiles = _instrumentsConfig.Profiles
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

        return Task.FromResult(dto);
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
            await PersistToDatabaseAsync(request, cancellationToken);
        }

        _instrumentsConfig.ReplaceProfiles(profiles);
        await _runtimeReconfigurationService.ApplySourcesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private async Task PersistToDatabaseAsync(SourceConfigDto request, CancellationToken cancellationToken)
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

        foreach (var profile in request.Profiles.Where(x => x.IsEnabled))
        {
            foreach (var symbol in profile.Symbols)
            {
                var (baseAsset, quoteAsset) = ParseSymbol(symbol);
                var isPerp = string.Equals(profile.MarketType, "Perp", StringComparison.OrdinalIgnoreCase);
                await connection.ExecuteAsync(new CommandDefinition(upsertSql, new
                {
                    profile.Exchange,
                    profile.MarketType,
                    profile.Transport,
                    Symbol = symbol,
                    BaseAsset = baseAsset,
                    QuoteAsset = quoteAsset,
                    Description = $"{symbol} {profile.MarketType}",
                    ContractSize = isPerp ? 1m : (decimal?)null,
                    profile.TargetUpdateIntervalMs
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
}

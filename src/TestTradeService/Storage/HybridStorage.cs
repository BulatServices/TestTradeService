using Dapper;
using TestTradeService.Interfaces;
using TestTradeService.Models;

namespace TestTradeService.Storage;

/// <summary>
/// Двухконтурная реализация хранилища: PostgreSQL для метаданных и TimescaleDB для таймсерий.
/// </summary>
public sealed class HybridStorage : IStorage
{
    private readonly MetadataDataSource _metadataDataSource;
    private readonly TimeseriesDataSource _timeseriesDataSource;

    /// <summary>
    /// Инициализирует двухконтурное хранилище.
    /// </summary>
    /// <param name="metadataDataSource">Источник подключений для метаданных.</param>
    /// <param name="timeseriesDataSource">Источник подключений для таймсерий.</param>
    public HybridStorage(MetadataDataSource metadataDataSource, TimeseriesDataSource timeseriesDataSource)
    {
        _metadataDataSource = metadataDataSource;
        _timeseriesDataSource = timeseriesDataSource;
    }

    /// <summary>
    /// Сохраняет raw-тик в TimescaleDB.
    /// </summary>
    /// <param name="tick">Нормализованный тик.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения тика.</returns>
    public async Task StoreTickAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into market.ticks("time", source, symbol, price, volume, fingerprint)
            values (@Timestamp, @Source, @Symbol, @Price, @Volume, @Fingerprint)
            on conflict (fingerprint, "time") do nothing
            """;

        await using var connection = await _timeseriesDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, tick, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Сохраняет агрегированную свечу в TimescaleDB.
    /// </summary>
    /// <param name="candle">Агрегированная свеча.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения свечи.</returns>
    public async Task StoreAggregateAsync(AggregatedCandle candle, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into market.candles("time", source, symbol, window_seconds, open, high, low, close, volume, count)
            values (@WindowStart, @Source, @Symbol, @WindowSeconds, @Open, @High, @Low, @Close, @Volume, @Count)
            on conflict (symbol, source, window_seconds, "time") do update
            set
                open = excluded.open,
                high = excluded.high,
                low = excluded.low,
                close = excluded.close,
                volume = excluded.volume,
                count = excluded.count
            """;

        var payload = new
        {
            candle.WindowStart,
            candle.Source,
            candle.Symbol,
            WindowSeconds = (int)candle.Window.TotalSeconds,
            candle.Open,
            candle.High,
            candle.Low,
            candle.Close,
            candle.Volume,
            candle.Count
        };

        await using var connection = await _timeseriesDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, payload, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Сохраняет метаданные инструмента в PostgreSQL.
    /// </summary>
    /// <param name="metadata">Метаданные инструмента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения метаданных.</returns>
    public async Task StoreInstrumentAsync(InstrumentMetadata metadata, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into meta.instruments
            (
                exchange,
                market_type,
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
                is_active,
                updated_at
            )
            values
            (
                @Exchange,
                @MarketType,
                @Symbol,
                @BaseAsset,
                @QuoteAsset,
                @Description,
                @PriceTickSize,
                @VolumeStep,
                @PriceDecimals,
                @VolumeDecimals,
                @ContractSize,
                @MinNotional,
                true,
                now()
            )
            on conflict (exchange, market_type, symbol) do update
            set
                base_asset = excluded.base_asset,
                quote_asset = excluded.quote_asset,
                description = excluded.description,
                price_tick_size = excluded.price_tick_size,
                volume_step = excluded.volume_step,
                price_decimals = excluded.price_decimals,
                volume_decimals = excluded.volume_decimals,
                contract_size = excluded.contract_size,
                min_notional = excluded.min_notional,
                is_active = excluded.is_active,
                updated_at = now()
            """;

        var payload = new
        {
            metadata.Exchange,
            MarketType = metadata.MarketType.ToString(),
            metadata.Symbol,
            metadata.BaseAsset,
            metadata.QuoteAsset,
            metadata.Description,
            metadata.PriceTickSize,
            metadata.VolumeStep,
            metadata.PriceDecimals,
            metadata.VolumeDecimals,
            metadata.ContractSize,
            metadata.MinNotional
        };

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, payload, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Сохраняет статус источника данных в PostgreSQL.
    /// </summary>
    /// <param name="status">Статус источника.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения статуса.</returns>
    public async Task StoreSourceStatusAsync(SourceStatus status, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into meta.source_status(exchange, source, is_online, last_update, message)
            values (@Exchange, @Source, @IsOnline, @LastUpdate, @Message)
            on conflict (exchange, source) do update
            set
                is_online = excluded.is_online,
                last_update = excluded.last_update,
                message = excluded.message
            """;

        var payload = new
        {
            Exchange = status.Exchange.ToString(),
            status.Source,
            status.IsOnline,
            status.LastUpdate,
            status.Message
        };

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, payload, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Сохраняет алерт в PostgreSQL.
    /// </summary>
    /// <param name="alert">Алерт.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача сохранения алерта.</returns>
    public async Task StoreAlertAsync(Alert alert, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into meta.alert_events(rule, source, symbol, message, "timestamp")
            values (@Rule, @Source, @Symbol, @Message, @Timestamp)
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, alert, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Загружает метаданные инструментов из PostgreSQL.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор активных инструментов.</returns>
    public async Task<IReadOnlyCollection<InstrumentMetadata>> GetInstrumentsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            select
                exchange as Exchange,
                market_type as MarketType,
                symbol as Symbol,
                base_asset as BaseAsset,
                quote_asset as QuoteAsset,
                description as Description,
                price_tick_size as PriceTickSize,
                volume_step as VolumeStep,
                price_decimals as PriceDecimals,
                volume_decimals as VolumeDecimals,
                contract_size as ContractSize,
                min_notional as MinNotional
            from meta.instruments
            where is_active = true
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<InstrumentRow>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();

        return rows.Select(row => new InstrumentMetadata
        {
            Exchange = row.Exchange,
            MarketType = Enum.Parse<MarketType>(row.MarketType, true),
            Symbol = row.Symbol,
            BaseAsset = row.BaseAsset,
            QuoteAsset = row.QuoteAsset,
            Description = row.Description,
            PriceTickSize = row.PriceTickSize,
            VolumeStep = row.VolumeStep,
            PriceDecimals = row.PriceDecimals,
            VolumeDecimals = row.VolumeDecimals,
            ContractSize = row.ContractSize,
            MinNotional = row.MinNotional
        }).ToArray();
    }

    /// <summary>
    /// Загружает конфигурации правил алертинга из PostgreSQL.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор конфигураций правил.</returns>
    public Task<IReadOnlyCollection<AlertRuleConfig>> GetAlertRulesAsync(CancellationToken cancellationToken)
    {
        var repository = new PostgresConfigurationRepository(_metadataDataSource);
        return repository.GetAlertRulesAsync(cancellationToken);
    }

    private sealed record InstrumentRow
    {
        public required string Exchange { get; init; }
        public required string MarketType { get; init; }
        public required string Symbol { get; init; }
        public required string BaseAsset { get; init; }
        public required string QuoteAsset { get; init; }
        public required string Description { get; init; }
        public required decimal PriceTickSize { get; init; }
        public required decimal VolumeStep { get; init; }
        public required int PriceDecimals { get; init; }
        public required int VolumeDecimals { get; init; }
        public decimal? ContractSize { get; init; }
        public decimal? MinNotional { get; init; }
    }
}

create table if not exists market.raw_ticks
(
    "time" timestamptz not null,
    received_at timestamptz not null,
    exchange text not null,
    source text not null,
    symbol text not null,
    market_type text not null,
    trade_id text null,
    price numeric not null,
    volume numeric not null,
    payload jsonb not null,
    metadata jsonb null,
    fingerprint text not null,
    constraint uq_market_raw_ticks_fingerprint_time unique (fingerprint, "time")
);

select create_hypertable('market.raw_ticks', 'time', if_not_exists => true, migrate_data => true);

create index if not exists ix_market_raw_ticks_source_symbol_time_desc
    on market.raw_ticks (source, symbol, "time" desc);

create index if not exists ix_market_raw_ticks_exchange_symbol_time_desc
    on market.raw_ticks (exchange, symbol, "time" desc);

alter table market.raw_ticks set (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'symbol,source'
);

select add_compression_policy('market.raw_ticks', compress_after => interval '7 days', if_not_exists => true);
select add_retention_policy('market.raw_ticks', drop_after => interval '30 days', if_not_exists => true);

alter table market.candles
    drop constraint if exists ck_market_candles_window_seconds;

alter table market.candles
    add constraint ck_market_candles_window_seconds
        check (window_seconds in (60, 300, 3600));

create index if not exists ix_market_candles_symbol_window_time_desc
    on market.candles (symbol, window_seconds, "time" desc);

create schema if not exists market;

create table if not exists market.ticks
(
    "time" timestamptz not null,
    source text not null,
    symbol text not null,
    price numeric not null,
    volume numeric not null,
    fingerprint text not null,
    constraint uq_market_ticks_fingerprint_time unique (fingerprint, "time")
);

select create_hypertable('market.ticks', 'time', if_not_exists => true, migrate_data => true);

create index if not exists ix_market_ticks_symbol_time_desc
    on market.ticks (symbol, "time" desc);

create index if not exists ix_market_ticks_source_time_desc
    on market.ticks (source, "time" desc);

create table if not exists market.candles
(
    "time" timestamptz not null,
    source text not null,
    symbol text not null,
    window_seconds int not null,
    open numeric not null,
    high numeric not null,
    low numeric not null,
    close numeric not null,
    volume numeric not null,
    count int not null,
    constraint uq_market_candles_key unique (symbol, source, window_seconds, "time")
);

select create_hypertable('market.candles', 'time', if_not_exists => true, migrate_data => true);

create schema if not exists meta;

create table if not exists meta.schema_migrations
(
    version text primary key,
    applied_at timestamptz not null default now()
);

create table if not exists meta.instruments
(
    id bigserial primary key,
    exchange text not null,
    market_type text not null,
    symbol text not null,
    base_asset text not null,
    quote_asset text not null,
    description text not null,
    price_tick_size numeric not null,
    volume_step numeric not null,
    price_decimals int not null,
    volume_decimals int not null,
    contract_size numeric null,
    min_notional numeric null,
    target_update_interval_ms int not null default 2000,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint uq_meta_instruments unique (exchange, market_type, symbol),
    constraint ck_meta_instruments_contract_size
        check
        (
            (market_type = 'Perp' and contract_size is not null and contract_size > 0)
            or
            (market_type <> 'Perp' and contract_size is null)
        )
);

create table if not exists meta.alert_rule_definitions
(
    id bigserial primary key,
    rule_name text not null,
    exchange text null,
    symbol text null,
    is_enabled boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_meta_alert_rule_definitions_lookup
    on meta.alert_rule_definitions (rule_name, exchange, symbol);

create unique index if not exists ux_meta_alert_rule_definitions_scope
    on meta.alert_rule_definitions (rule_name, coalesce(exchange, ''), coalesce(symbol, ''));

create table if not exists meta.alert_rule_parameters
(
    id bigserial primary key,
    rule_definition_id bigint not null references meta.alert_rule_definitions (id) on delete cascade,
    param_key text not null,
    param_value text not null,
    updated_at timestamptz not null default now(),
    constraint uq_meta_alert_rule_parameters unique (rule_definition_id, param_key)
);

create table if not exists meta.source_status
(
    id bigserial primary key,
    exchange text not null,
    source text not null,
    is_online boolean not null,
    last_update timestamptz not null,
    message text null,
    constraint uq_meta_source_status unique (exchange, source)
);

create table if not exists meta.alert_events
(
    id bigserial primary key,
    rule text not null,
    source text not null,
    symbol text not null,
    message text not null,
    "timestamp" timestamptz not null,
    created_at timestamptz not null default now()
);

create index if not exists ix_meta_alert_events_timestamp_desc
    on meta.alert_events ("timestamp" desc);

create index if not exists ix_meta_alert_events_symbol_timestamp_desc
    on meta.alert_events (symbol, "timestamp" desc);

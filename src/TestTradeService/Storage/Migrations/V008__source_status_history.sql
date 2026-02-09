create table if not exists meta.source_status_events
(
    id bigserial primary key,
    exchange text not null,
    source text not null,
    is_online boolean not null,
    last_update timestamptz not null,
    message text null,
    changed_at timestamptz not null default now()
);

create index if not exists ix_meta_source_status_events_exchange_source_changed_at_desc
    on meta.source_status_events (exchange, source, changed_at desc);

create index if not exists ix_meta_source_status_events_changed_at_desc
    on meta.source_status_events (changed_at desc);

create index if not exists ix_meta_instruments_profile_all
    on meta.instruments (exchange, market_type, transport, is_active);

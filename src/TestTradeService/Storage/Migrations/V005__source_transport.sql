alter table meta.instruments
    add column if not exists transport text not null default 'WebSocket';

update meta.instruments
set transport = case
    when exchange = 'Demo' and market_type = 'Spot' then 'Rest'
    else 'WebSocket'
end
where transport is null or transport = '';

create index if not exists ix_meta_instruments_profile
    on meta.instruments (exchange, market_type, transport)
    where is_active = true;

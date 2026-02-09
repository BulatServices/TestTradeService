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
    target_update_interval_ms,
    is_active,
    updated_at
)
values
('Kraken', 'Spot', 'XBT/USD', 'XBT', 'USD', 'XBT/USD Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Kraken', 'Spot', 'ETH/USD', 'ETH', 'USD', 'ETH/USD Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Coinbase', 'Spot', 'BTC-USD', 'BTC', 'USD', 'BTC-USD Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Coinbase', 'Spot', 'ETH-USD', 'ETH', 'USD', 'ETH-USD Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Coinbase', 'Spot', 'SOL-USD', 'SOL', 'USD', 'SOL-USD Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Bybit', 'Spot', 'BTCUSDT', 'BTC', 'USDT', 'BTCUSDT Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Bybit', 'Spot', 'ETHUSDT', 'ETH', 'USDT', 'ETHUSDT Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now()),
('Bybit', 'Spot', 'SOLUSDT', 'SOL', 'USDT', 'SOLUSDT Spot', 0.01, 0.0001, 2, 4, null, 10, 2000, true, now())
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
    target_update_interval_ms = excluded.target_update_interval_ms,
    is_active = excluded.is_active,
    updated_at = now();

insert into meta.alert_rule_definitions(rule_name, exchange, symbol, is_enabled, updated_at)
values
    ('PriceThreshold', null, null, true, now()),
    ('VolumeSpike', null, null, true, now())
on conflict do nothing;

insert into meta.alert_rule_parameters(rule_definition_id, param_key, param_value, updated_at)
select d.id, p.param_key, p.param_value, now()
from meta.alert_rule_definitions d
join
(
    values
        ('PriceThreshold', 'min_price', '18000'),
        ('PriceThreshold', 'max_price', '22000'),
        ('VolumeSpike', 'min_volume', '4'),
        ('VolumeSpike', 'min_count', '5')
) as p(rule_name, param_key, param_value)
    on p.rule_name = d.rule_name
where d.exchange is null and d.symbol is null
on conflict (rule_definition_id, param_key) do update
set
    param_value = excluded.param_value,
    updated_at = now();

delete from meta.alert_rule_parameters
where rule_definition_id in
(
    select id
    from meta.alert_rule_definitions
    where rule_name = 'PriceThreshold'
      and exchange is null
      and symbol is null
);

delete from meta.alert_rule_definitions
where rule_name = 'PriceThreshold'
  and exchange is null
  and symbol is null;

insert into meta.alert_rule_definitions(rule_name, exchange, symbol, is_enabled, updated_at)
select
    'PriceThreshold',
    i.exchange,
    i.symbol,
    true,
    now()
from meta.instruments i
where i.is_active = true
on conflict (rule_name, coalesce(exchange, ''), coalesce(symbol, '')) do update
set
    is_enabled = excluded.is_enabled,
    updated_at = now();

with scoped_rules as
(
    select
        d.id,
        i.base_asset
    from meta.alert_rule_definitions d
    join meta.instruments i
      on i.exchange = d.exchange
     and i.symbol = d.symbol
    where d.rule_name = 'PriceThreshold'
      and d.exchange is not null
      and d.symbol is not null
      and i.is_active = true
),
scoped_parameters as
(
    select
        sr.id as rule_definition_id,
        p.param_key,
        p.param_value
    from scoped_rules sr
    cross join lateral
    (
        values
            (
                'min_price',
                case upper(sr.base_asset)
                    when 'BTC' then '10000'
                    when 'XBT' then '10000'
                    when 'ETH' then '500'
                    when 'SOL' then '10'
                    else '0'
                end
            ),
            (
                'max_price',
                case upper(sr.base_asset)
                    when 'BTC' then '200000'
                    when 'XBT' then '200000'
                    when 'ETH' then '10000'
                    when 'SOL' then '1000'
                    else '1000000000'
                end
            )
    ) as p(param_key, param_value)
)
insert into meta.alert_rule_parameters(rule_definition_id, param_key, param_value, updated_at)
select
    sp.rule_definition_id,
    sp.param_key,
    sp.param_value,
    now()
from scoped_parameters sp
on conflict (rule_definition_id, param_key) do update
set
    param_value = excluded.param_value,
    updated_at = now();

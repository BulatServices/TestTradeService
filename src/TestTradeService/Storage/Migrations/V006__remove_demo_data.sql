delete from meta.instruments
where exchange = 'Demo';

delete from meta.source_status
where exchange = 'Demo';

delete from meta.alert_rule_definitions
where exchange = 'Demo';

delete from market.ticks
where source ilike 'Demo-%' or source = 'Legacy-WebSocket';

delete from market.candles
where source ilike 'Demo-%' or source = 'Legacy-WebSocket';

alter table market.ticks set (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'symbol,source'
);

select add_compression_policy('market.ticks', compress_after => interval '7 days', if_not_exists => true);
select add_retention_policy('market.ticks', drop_after => interval '30 days', if_not_exists => true);
select add_retention_policy('market.candles', drop_after => interval '2 years', if_not_exists => true);

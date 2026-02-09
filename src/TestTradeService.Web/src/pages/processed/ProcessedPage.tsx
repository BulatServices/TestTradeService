import { useMemo, useState } from 'react';
import { Card, DatePicker, Select, Space, Table, Tabs, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { getCandles, getMetrics } from '../../features/processed/api/processedApi';
import { CandleDto } from '../../entities/processed/model/types';
import { formatDateTime, formatNumber } from '../../shared/lib/format';

const { RangePicker } = DatePicker;
const blockedExchangeValues = new Set(['demo']);
const MAX_CHART_CANDLES = 120;

type ProcessedChartCandle = {
  key: string;
  timeLabel: string;
  timestamp: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  rising: boolean;
};

function buildProcessedCandles(candles: CandleDto[], symbol?: string): {
  symbol?: string;
  candles: ProcessedChartCandle[];
  min: number;
  max: number;
} {
  if (!symbol) {
    return { symbol: undefined, candles: [], min: 0, max: 0 };
  }

  const normalized = symbol.trim().toUpperCase();
  const filtered = candles
    .filter((item) => item.symbol.trim().toUpperCase() === normalized)
    .sort((a, b) => new Date(a.windowStart).getTime() - new Date(b.windowStart).getTime())
    .slice(-MAX_CHART_CANDLES);

  if (!filtered.length) {
    return { symbol, candles: [], min: 0, max: 0 };
  }

  const series = filtered.map((item, index) => ({
    key: `${item.exchange}-${item.symbol}-${item.windowStart}-${index}`,
    timeLabel: new Date(item.windowStart).toLocaleTimeString('ru-RU'),
    timestamp: item.windowStart,
    open: item.open,
    high: item.high,
    low: item.low,
    close: item.close,
    volume: item.volume,
    rising: item.close >= item.open
  }));

  return {
    symbol,
    candles: series,
    min: Math.min(...series.map((item) => item.low)),
    max: Math.max(...series.map((item) => item.high))
  };
}

function ProcessedCandlestickChart({
  symbol,
  candles,
  min,
  max
}: {
  symbol: string;
  candles: ProcessedChartCandle[];
  min: number;
  max: number;
}) {
  const width = Math.max(720, candles.length * 18);
  const height = 360;
  const padding = { top: 20, right: 78, bottom: 32, left: 16 };
  const volumeAreaHeight = 86;
  const priceAreaHeight = height - padding.top - padding.bottom - volumeAreaHeight;
  const priceTop = padding.top;
  const volumeTop = priceTop + priceAreaHeight;
  const chartWidth = width - padding.left - padding.right;
  const slot = chartWidth / Math.max(candles.length, 1);
  const bodyWidth = Math.min(14, Math.max(4, slot * 0.62));
  const span = Math.max(max - min, Number.EPSILON);
  const paddedMin = min - span * 0.06;
  const paddedMax = max + span * 0.06;
  const paddedSpan = Math.max(paddedMax - paddedMin, Number.EPSILON);
  const maxVolume = Math.max(...candles.map((item) => item.volume), Number.EPSILON);

  const yForPrice = (price: number) => priceTop + ((paddedMax - price) / paddedSpan) * priceAreaHeight;
  const yForVolume = (volume: number) => volumeTop + volumeAreaHeight - (volume / maxVolume) * (volumeAreaHeight - 6);

  const priceLevels = Array.from({ length: 5 }, (_, index) => {
    const value = paddedMax - ((paddedMax - paddedMin) * index) / 4;
    return {
      value,
      y: yForPrice(value)
    };
  });

  const timeLabels = candles.length <= 1
    ? []
    : Array.from({ length: Math.min(6, candles.length) }, (_, index) => {
      const candleIndex = Math.round((index * (candles.length - 1)) / Math.max(Math.min(6, candles.length) - 1, 1));
      const candle = candles[candleIndex];
      const x = padding.left + candleIndex * slot + slot / 2;
      return { key: candle.key, x, label: candle.timeLabel };
    });

  return (
    <div style={{ border: '1px solid #f0f0f0', borderRadius: 8, padding: 12 }}>
      <div style={{ marginBottom: 8, fontSize: 12, color: '#595959' }}>
        {symbol}: O/H/L/C последней свечи {formatNumber(candles[candles.length - 1].open, 4)} / {formatNumber(candles[candles.length - 1].high, 4)} / {formatNumber(candles[candles.length - 1].low, 4)} / {formatNumber(candles[candles.length - 1].close, 4)}
      </div>
      <div style={{ overflowX: 'auto' }}>
        <svg width={width} height={height} role="img" aria-label="Свечной график processed">
          <rect x={padding.left} y={priceTop} width={chartWidth} height={priceAreaHeight} fill="#ffffff" rx={8} />
          <rect x={padding.left} y={volumeTop} width={chartWidth} height={volumeAreaHeight} fill="#f7f9fc" rx={8} />

          {priceLevels.map((level) => (
            <g key={String(level.value)}>
              <line
                x1={padding.left}
                x2={padding.left + chartWidth}
                y1={level.y}
                y2={level.y}
                stroke="#ebedf3"
                strokeDasharray="4 4"
              />
              <text x={padding.left + chartWidth + 10} y={level.y + 4} fill="#6d7385" fontSize="11">
                {formatNumber(level.value, 4)}
              </text>
            </g>
          ))}

          {candles.map((candle, index) => {
            const color = candle.rising ? '#1ea672' : '#e24f4f';
            const centerX = padding.left + index * slot + slot / 2;
            const wickTop = yForPrice(candle.high);
            const wickBottom = yForPrice(candle.low);
            const openY = yForPrice(candle.open);
            const closeY = yForPrice(candle.close);
            const bodyTop = Math.min(openY, closeY);
            const bodyHeight = Math.max(Math.abs(openY - closeY), 1.4);
            const volumeY = yForVolume(candle.volume);

            return (
              <g key={candle.key}>
                <line x1={centerX} x2={centerX} y1={wickTop} y2={wickBottom} stroke={color} strokeWidth={1.5} />
                <rect x={centerX - bodyWidth / 2} y={bodyTop} width={bodyWidth} height={bodyHeight} fill={color} rx={2} />
                <rect
                  x={centerX - bodyWidth / 2}
                  y={volumeY}
                  width={bodyWidth}
                  height={Math.max(volumeTop + volumeAreaHeight - volumeY, 1)}
                  fill={color}
                  opacity={0.35}
                  rx={2}
                />
              </g>
            );
          })}

          {timeLabels.map((item) => (
            <text key={item.key} x={item.x} y={height - 8} textAnchor="middle" fontSize="10" fill="#8c8c8c">
              {item.label}
            </text>
          ))}
        </svg>
      </div>
    </div>
  );
}

export function ProcessedPage() {
  const [exchange, setExchange] = useState<string>();
  const [symbol, setSymbol] = useState<string>();
  const [windowValue, setWindowValue] = useState('1m');
  const [period, setPeriod] = useState<[string, string] | undefined>();
  const [page, setPage] = useState(1);
  const safeExchange = exchange && blockedExchangeValues.has(exchange.toLowerCase()) ? undefined : exchange;

  const commonParams = {
    exchange: safeExchange,
    symbol,
    window: windowValue,
    dateFrom: period?.[0],
    dateTo: period?.[1]
  };

  const candlesQuery = useQuery({
    queryKey: ['candles', commonParams, page],
    queryFn: () => getCandles({ ...commonParams, page, pageSize: 20 })
  });

  const metricsQuery = useQuery({
    queryKey: ['metrics', commonParams],
    queryFn: () => getMetrics(commonParams)
  });

  const candleChart = useMemo(() => buildProcessedCandles(candlesQuery.data?.items ?? [], symbol), [candlesQuery.data, symbol]);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={3}>Обработанные данные</Typography.Title>

      <Card>
        <Space wrap>
          <Select
            placeholder="Биржа"
            allowClear
            style={{ width: 180 }}
            value={safeExchange}
            onChange={(value) => {
              if (value && blockedExchangeValues.has(value.toLowerCase())) {
                setExchange(undefined);
                return;
              }

              setExchange(value);
            }}
            options={['Kraken', 'Coinbase', 'Bybit'].map((value) => ({ value }))}
          />
          <Select
            placeholder="Тикер"
            allowClear
            style={{ width: 180 }}
            value={symbol}
            onChange={setSymbol}
            options={['BTC-USD', 'ETH-USD', 'SOL-USD', 'XBT/USD'].map((value) => ({ value }))}
          />
          <Select
            placeholder="Окно"
            style={{ width: 120 }}
            value={windowValue}
            onChange={setWindowValue}
            options={['1m', '5m', '1h'].map((value) => ({ value }))}
          />
          <RangePicker
            showTime
            onChange={(values) => {
              if (!values || values.length !== 2) {
                setPeriod(undefined);
                return;
              }

              const [start, end] = values;
              if (!start || !end) {
                setPeriod(undefined);
                return;
              }

              setPeriod([start.toISOString(), end.toISOString()]);
            }}
          />
        </Space>
      </Card>

      <Tabs
        items={[
          {
            key: 'candles',
            label: 'Свечи',
            children: (
              <Card loading={candlesQuery.isLoading}>
                <Table
                  rowKey={(row) => `${row.symbol}-${row.windowStart}`}
                  dataSource={candlesQuery.data?.items ?? []}
                  pagination={{
                    current: page,
                    pageSize: 20,
                    total: candlesQuery.data?.total ?? 0,
                    onChange: (next) => setPage(next)
                  }}
                  locale={{
                    emptyText: candlesQuery.isError
                      ? 'Ошибка загрузки свечей'
                      : 'Нет данных по выбранным фильтрам'
                  }}
                  columns={[
                    { title: 'Источник', dataIndex: 'source' },
                    { title: 'Биржа', dataIndex: 'exchange' },
                    { title: 'Тикер', dataIndex: 'symbol' },
                    {
                      title: 'Окно',
                      render: (_, row) => `${row.window} (${formatDateTime(row.windowStart)})`
                    },
                    { title: 'Открытие', dataIndex: 'open', render: (value) => formatNumber(value, 4) },
                    { title: 'Максимум', dataIndex: 'high', render: (value) => formatNumber(value, 4) },
                    { title: 'Минимум', dataIndex: 'low', render: (value) => formatNumber(value, 4) },
                    { title: 'Закрытие', dataIndex: 'close', render: (value) => formatNumber(value, 4) },
                    { title: 'Объём', dataIndex: 'volume', render: (value) => formatNumber(value, 4) },
                    { title: 'Количество тиков', dataIndex: 'count' }
                  ]}
                />

                <div style={{ width: '100%', marginTop: 16 }}>
                  {!symbol ? (
                    <Typography.Text type="secondary">Выберите тикер в фильтре выше, чтобы построить свечной график.</Typography.Text>
                  ) : candleChart.candles.length ? (
                    <ProcessedCandlestickChart
                      symbol={symbol}
                      candles={candleChart.candles}
                      min={candleChart.min}
                      max={candleChart.max}
                    />
                  ) : (
                    <Typography.Text type="secondary">Недостаточно данных для свечного графика по выбранному тикеру.</Typography.Text>
                  )}
                </div>
              </Card>
            )
          },
          {
            key: 'metrics',
            label: 'Метрики',
            children: (
              <Card loading={metricsQuery.isLoading}>
                <Table
                  rowKey={(row) => `${row.symbol}-${row.windowStart}`}
                  dataSource={metricsQuery.data?.items ?? []}
                  locale={{
                    emptyText: metricsQuery.isError
                      ? 'Ошибка загрузки метрик'
                      : 'Нет метрик по выбранным фильтрам'
                  }}
                  pagination={{ pageSize: 20 }}
                  columns={[
                    { title: 'Тикер', dataIndex: 'symbol' },
                    { title: 'Начало окна', dataIndex: 'windowStart', render: (value) => formatDateTime(value) },
                    { title: 'Окно', dataIndex: 'window' },
                    {
                      title: 'Средняя цена',
                      dataIndex: 'averagePrice',
                      render: (value) => formatNumber(value, 4)
                    },
                    {
                      title: 'Волатильность',
                      dataIndex: 'volatility',
                      render: (value) => formatNumber(value, 4)
                    },
                    { title: 'Количество тиков', dataIndex: 'count' }
                  ]}
                />
              </Card>
            )
          }
        ]}
      />
    </Space>
  );
}

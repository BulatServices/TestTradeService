import { useMemo, useState } from 'react';
import { Card, DatePicker, Select, Space, Table, Tabs, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { getCandles, getMetrics } from '../../features/processed/api/processedApi';
import { formatDateTime, formatNumber } from '../../shared/lib/format';

const { RangePicker } = DatePicker;

export function ProcessedPage() {
  const [exchange, setExchange] = useState<string>();
  const [symbol, setSymbol] = useState<string>();
  const [windowValue, setWindowValue] = useState('1m');
  const [period, setPeriod] = useState<[string, string] | undefined>();
  const [page, setPage] = useState(1);

  const commonParams = {
    exchange,
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

  const candleChart = useMemo(() => {
    const items = candlesQuery.data?.items ?? [];
    if (!items.length) {
      return {
        max: 0,
        min: 0,
        candles: [] as Array<{
          key: string;
          time: string;
          highTop: number;
          wickHeight: number;
          bodyTop: number;
          bodyHeight: number;
          rising: boolean;
          open: number;
          high: number;
          low: number;
          close: number;
        }>
      };
    }

    const max = Math.max(...items.map((item) => item.high));
    const min = Math.min(...items.map((item) => item.low));
    const span = Math.max(max - min, Number.EPSILON);

    const toPercentFromTop = (price: number) => ((max - price) / span) * 100;

    return {
      max,
      min,
      candles: items.map((item) => {
        const highTop = toPercentFromTop(item.high);
        const lowTop = toPercentFromTop(item.low);
        const openTop = toPercentFromTop(item.open);
        const closeTop = toPercentFromTop(item.close);

        return {
          key: `${item.symbol}-${item.windowStart}`,
          time: new Date(item.windowStart).toLocaleTimeString('ru-RU'),
          highTop,
          wickHeight: Math.max(lowTop - highTop, 1),
          bodyTop: Math.min(openTop, closeTop),
          bodyHeight: Math.max(Math.abs(openTop - closeTop), 1),
          rising: item.close >= item.open,
          open: item.open,
          high: item.high,
          low: item.low,
          close: item.close
        };
      })
    };
  }, [candlesQuery.data]);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={3}>Обработанные данные</Typography.Title>

      <Card>
        <Space wrap>
          <Select
            placeholder="Биржа"
            allowClear
            style={{ width: 180 }}
            value={exchange}
            onChange={setExchange}
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
                  {candleChart.candles.length ? (
                    <div style={{ border: '1px solid #f0f0f0', borderRadius: 8, padding: 12 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8, fontSize: 12, color: '#595959' }}>
                        <span>max: {formatNumber(candleChart.max, 4)}</span>
                        <span>min: {formatNumber(candleChart.min, 4)}</span>
                      </div>
                      <div
                        style={{
                          display: 'flex',
                          gap: 6,
                          height: 220,
                          minWidth: 420,
                          overflowX: 'auto',
                          alignItems: 'stretch'
                        }}
                      >
                        {candleChart.candles.map((item) => {
                          const color = item.rising ? '#52c41a' : '#ff4d4f';
                          return (
                            <div key={item.key} style={{ flex: 1, minWidth: 16, position: 'relative' }} title={`O: ${formatNumber(item.open, 4)} H: ${formatNumber(item.high, 4)} L: ${formatNumber(item.low, 4)} C: ${formatNumber(item.close, 4)}`}>
                              <div
                                style={{
                                  position: 'absolute',
                                  left: '50%',
                                  marginLeft: -1,
                                  width: 2,
                                  top: `${item.highTop}%`,
                                  height: `${item.wickHeight}%`,
                                  backgroundColor: color
                                }}
                              />
                              <div
                                style={{
                                  position: 'absolute',
                                  left: '50%',
                                  marginLeft: -5,
                                  width: 10,
                                  top: `${item.bodyTop}%`,
                                  height: `${item.bodyHeight}%`,
                                  backgroundColor: color,
                                  borderRadius: 2
                                }}
                              />
                              <div
                                style={{
                                  position: 'absolute',
                                  bottom: -20,
                                  left: '50%',
                                  transform: 'translateX(-50%)',
                                  fontSize: 11,
                                  color: '#8c8c8c',
                                  whiteSpace: 'nowrap'
                                }}
                              >
                                {item.time}
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  ) : (
                    <Typography.Text type="secondary">Недостаточно данных для построения свечного графика.</Typography.Text>
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


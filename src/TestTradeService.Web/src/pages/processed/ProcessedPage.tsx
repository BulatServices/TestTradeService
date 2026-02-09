import { useMemo, useState } from 'react';
import { Card, DatePicker, Select, Space, Table, Tabs, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { getCandles, getMetrics } from '../../features/processed/api/processedApi';
import { formatDateTime, formatNumber } from '../../shared/lib/format';
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

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

  const chartData = useMemo(
    () =>
      (candlesQuery.data?.items ?? []).map((item) => ({
        time: new Date(item.windowStart).toLocaleTimeString('ru-RU'),
        close: item.close,
        volume: item.volume
      })),
    [candlesQuery.data]
  );

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
                <div style={{ width: '100%', height: 280, marginTop: 16 }}>
                  <ResponsiveContainer>
                    <LineChart data={chartData}>
                      <XAxis dataKey="time" />
                      <YAxis yAxisId="close" orientation="left" />
                      <YAxis yAxisId="volume" orientation="right" />
                      <Tooltip />
                      <Line yAxisId="close" dataKey="close" stroke="#fa8c16" dot={false} name="Цена" />
                      <Line yAxisId="volume" dataKey="volume" stroke="#13c2c2" dot={false} name="Объём" />
                    </LineChart>
                  </ResponsiveContainer>
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


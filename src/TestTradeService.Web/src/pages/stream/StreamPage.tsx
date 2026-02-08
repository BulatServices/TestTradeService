import { useEffect, useMemo, useRef, useState } from 'react';
import { Card, Col, Row, Select, Space, Statistic, Table, Typography, Button } from 'antd';
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { tickEventSchema, TickEventDto } from '../../entities/stream/model/types';
import { useMarketHubContext } from '../../shared/signalr/MarketHubProvider';
import { RingBuffer } from '../../shared/lib/ringBuffer';
import { formatDateTime, formatNumber } from '../../shared/lib/format';
import { percentile } from '../../shared/lib/stats';

const MAX_TICKS = 500;

type StreamTickRow = TickEventDto & {
  rowId: string;
};

export function StreamPage() {
  const { connection, reconnectCount } = useMarketHubContext();
  const bufferRef = useRef(new RingBuffer<StreamTickRow>(MAX_TICKS));
  const rowIdRef = useRef(0);
  const [ticks, setTicks] = useState<StreamTickRow[]>([]);
  const [paused, setPaused] = useState(false);
  const [exchangeFilter, setExchangeFilter] = useState<string>();
  const [symbolFilter, setSymbolFilter] = useState<string>();
  const [messagesPerSecond, setMessagesPerSecond] = useState(0);
  const [delays, setDelays] = useState<number[]>([]);

  useEffect(() => {
    if (!connection) {
      return;
    }

    let counter = 0;
    const timer = setInterval(() => {
      setMessagesPerSecond(counter);
      counter = 0;
    }, 1000);

    const tickHandler = (payload: unknown) => {
      const parsed = tickEventSchema.safeParse(payload);
      if (!parsed.success) {
        return;
      }

      const tick = parsed.data;
      counter += 1;

      const delayMs = Math.max(0, new Date(tick.receivedAt).getTime() - new Date(tick.timestamp).getTime());
      setDelays((prev) => [...prev.slice(-499), delayMs]);

      if (!paused) {
        const rowId = tick.id ?? `${tick.source}-${tick.symbol}-${tick.timestamp}-${rowIdRef.current}`;
        if (!tick.id) {
          rowIdRef.current += 1;
        }
        bufferRef.current.push({ ...tick, rowId });
        setTicks(bufferRef.current.toArray());
      }
    };

    connection.on('tick', tickHandler);

    return () => {
      clearInterval(timer);
      connection.off('tick', tickHandler);
    };
  }, [connection, paused]);

  useEffect(() => {
    if (!connection) {
      return;
    }

    const applyFilter = async () => {
      try {
        await connection.invoke('SetStreamFilter', {
          exchange: exchangeFilter ?? null,
          symbol: symbolFilter ?? null
        });

        if (symbolFilter) {
          await connection.invoke('SubscribeSymbols', [symbolFilter]);
        }
      } catch (error) {
        console.warn('Не удалось применить фильтр потока', error);
      }
    };

    void applyFilter();

    return () => {
      if (symbolFilter) {
        void connection.invoke('UnsubscribeSymbols', [symbolFilter]);
      }
    };
  }, [connection, exchangeFilter, symbolFilter]);

  const filteredTicks = useMemo(
    () =>
      ticks.filter((tick) => {
        if (exchangeFilter && tick.exchange !== exchangeFilter) {
          return false;
        }

        if (symbolFilter && tick.symbol !== symbolFilter) {
          return false;
        }

        return true;
      }),
    [ticks, exchangeFilter, symbolFilter]
  );

  const chartData = filteredTicks.slice(-50).map((item) => ({
    time: new Date(item.timestamp).toLocaleTimeString('ru-RU'),
    price: item.price,
    volume: item.volume
  }));

  const exchanges = Array.from(new Set(ticks.map((item) => item.exchange)));
  const symbols = Array.from(new Set(ticks.map((item) => item.symbol)));

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={3}>Поток данных в реальном времени</Typography.Title>

      <Row gutter={16}>
        <Col xs={24} md={6}>
          <Card>
            <Statistic title="Сообщений в секунду" value={messagesPerSecond} />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card>
            <Statistic title="Задержка p50 (мс)" value={formatNumber(percentile(delays, 50), 0)} />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card>
            <Statistic title="Задержка p95 (мс)" value={formatNumber(percentile(delays, 95), 0)} />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card>
            <Statistic title="Переподключений" value={reconnectCount} />
          </Card>
        </Col>
      </Row>

      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            placeholder="Фильтр по бирже"
            allowClear
            value={exchangeFilter}
            onChange={setExchangeFilter}
            options={exchanges.map((value) => ({ label: value, value }))}
            style={{ width: 220 }}
          />
          <Select
            placeholder="Фильтр по тикеру"
            allowClear
            value={symbolFilter}
            onChange={setSymbolFilter}
            options={symbols.map((value) => ({ label: value, value }))}
            style={{ width: 220 }}
          />
          <Button onClick={() => setPaused(true)} disabled={paused}>
            Пауза ленты
          </Button>
          <Button onClick={() => setPaused(false)} disabled={!paused}>
            Возобновить
          </Button>
          <Button
            onClick={() => {
              bufferRef.current.clear();
              setTicks([]);
              setDelays([]);
            }}
          >
            Очистить
          </Button>
        </Space>

        <Table
          size="small"
          rowKey="rowId"
          dataSource={filteredTicks.slice().reverse()}
          pagination={{ pageSize: 15 }}
          columns={[
            { title: 'Источник', dataIndex: 'source' },
            { title: 'Биржа', dataIndex: 'exchange' },
            { title: 'Тикер', dataIndex: 'symbol' },
            { title: 'Цена', dataIndex: 'price', render: (value) => formatNumber(value, 4) },
            { title: 'Объём', dataIndex: 'volume', render: (value) => formatNumber(value, 4) },
            {
              title: 'Время',
              dataIndex: 'timestamp',
              render: (value) => formatDateTime(value)
            }
          ]}
        />
      </Card>

      <Card title="Мини-график цены и объёма">
        <div style={{ width: '100%', height: 280 }}>
          <ResponsiveContainer>
            <LineChart data={chartData}>
              <XAxis dataKey="time" />
              <YAxis yAxisId="price" orientation="left" />
              <YAxis yAxisId="volume" orientation="right" />
              <Tooltip />
              <Line yAxisId="price" dataKey="price" stroke="#1890ff" dot={false} name="Цена" />
              <Line yAxisId="volume" dataKey="volume" stroke="#52c41a" dot={false} name="Объём" />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </Card>
    </Space>
  );
}


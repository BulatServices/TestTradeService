import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Col, Row, Select, Space, Statistic, Table, Typography } from 'antd';
import { tickEventSchema, TickEventDto } from '../../entities/stream/model/types';
import { useMarketHubContext } from '../../shared/signalr/MarketHubProvider';
import { RingBuffer } from '../../shared/lib/ringBuffer';
import { formatDateTime, formatNumber } from '../../shared/lib/format';
import { percentile } from '../../shared/lib/stats';

const MAX_TICKS = 500;
const MAX_CHART_TICKS = 120;
const DEFAULT_TICKS_PER_CANDLE = 5;
const MAX_PERFORMANCE_POINTS = 120;
const PERFORMANCE_Y_TICKS = 5;

type StreamTickRow = TickEventDto & {
  rowId: string;
};

type StreamCandle = {
  key: string;
  time: string;
  timestamp: string;
  rising: boolean;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
};

type PerformancePoint = {
  key: string;
  timeLabel: string;
  messagesPerSecond: number;
  delayP95Ms: number;
};

type PerformanceYAxisTick = {
  key: string;
  y: number;
  messagesValue: number;
  delayValue: number;
};

function normalizeSymbol(value: string): string {
  return value.trim().toUpperCase();
}

export function calculateClientDelayMs(tickTimestamp: string, clientReceivedAtMs: number): number {
  const tickTimestampMs = new Date(tickTimestamp).getTime();
  return Math.max(0, clientReceivedAtMs - tickTimestampMs);
}

export function getDeterministicChartSymbol(ticks: StreamTickRow[], selectedSymbol?: string): string | undefined {
  if (selectedSymbol) {
    return selectedSymbol;
  }

  const symbols = Array.from(new Set(ticks.map((item) => item.symbol)))
    .filter((item) => item.trim().length > 0)
    .sort((a, b) => a.localeCompare(b, 'en', { sensitivity: 'base' }));

  return symbols[0];
}

export function buildCandles(
  ticks: StreamTickRow[],
  chartSymbol?: string,
  ticksPerCandle = DEFAULT_TICKS_PER_CANDLE
): { symbol?: string; candles: StreamCandle[]; max: number; min: number } {
  if (!chartSymbol) {
    return { symbol: undefined, candles: [], max: 0, min: 0 };
  }

  const symbolKey = normalizeSymbol(chartSymbol);
  const symbolTicks = ticks
    .filter((item) => normalizeSymbol(item.symbol) === symbolKey)
    .slice(-MAX_CHART_TICKS);

  if (!symbolTicks.length) {
    return { symbol: chartSymbol, candles: [], max: 0, min: 0 };
  }

  const candles: StreamCandle[] = [];
  const safeTicksPerCandle = Math.max(1, Math.floor(ticksPerCandle));

  for (let i = 0; i < symbolTicks.length; i += safeTicksPerCandle) {
    const chunk = symbolTicks.slice(i, i + safeTicksPerCandle);
    const firstTick = chunk[0];
    const lastTick = chunk[chunk.length - 1];
    const prices = chunk.map((item) => item.price);
    const totalVolume = chunk.reduce((sum, item) => sum + item.volume, 0);

    candles.push({
      key: `${symbolKey}-${i}-${lastTick.timestamp}`,
      time: new Date(lastTick.timestamp).toLocaleTimeString('ru-RU'),
      timestamp: lastTick.timestamp,
      open: firstTick.price,
      high: Math.max(...prices),
      low: Math.min(...prices),
      close: lastTick.price,
      volume: totalVolume,
      rising: lastTick.price >= firstTick.price
    });
  }

  return {
    symbol: chartSymbol,
    max: Math.max(...candles.map((item) => item.high)),
    min: Math.min(...candles.map((item) => item.low)),
    candles
  };
}

export function StreamPage() {
  const { connection, connectionState, reconnectCount } = useMarketHubContext();
  const bufferRef = useRef(new RingBuffer<StreamTickRow>(MAX_TICKS));
  const rowIdRef = useRef(0);
  const [ticks, setTicks] = useState<StreamTickRow[]>([]);
  const [paused, setPaused] = useState(false);
  const [exchangeFilter, setExchangeFilter] = useState<string>();
  const [symbolFilter, setSymbolFilter] = useState<string>();
  const [messagesPerSecond, setMessagesPerSecond] = useState(0);
  const [delays, setDelays] = useState<number[]>([]);
  const [performanceHistory, setPerformanceHistory] = useState<PerformancePoint[]>([]);

  useEffect(() => {
    if (!connection || connectionState !== 'Подключено') {
      return;
    }

    const subscribeStream = async () => {
      try {
        await connection.invoke('SubscribeStream');
      } catch (error) {
        console.warn('Не удалось подписаться на поток', error);
      }
    };

    void subscribeStream();

    return () => {
      void connection.invoke('UnsubscribeStream');
    };
  }, [connection, connectionState]);

  useEffect(() => {
    if (!connection || connectionState !== 'Подключено') {
      return;
    }

    let counter = 0;
    let secondDelays: number[] = [];

    const timer = setInterval(() => {
      setMessagesPerSecond(counter);

      const nowIso = new Date().toISOString();
      const delayP95Ms = percentile(secondDelays, 95);
      setPerformanceHistory((prev) =>
        [
          ...prev,
          {
            key: nowIso,
            timeLabel: new Date(nowIso).toLocaleTimeString('ru-RU'),
            messagesPerSecond: counter,
            delayP95Ms
          }
        ].slice(-MAX_PERFORMANCE_POINTS)
      );

      counter = 0;
      secondDelays = [];
    }, 1000);

    const tickHandler = (payload: unknown) => {
      const parsed = tickEventSchema.safeParse(payload);
      if (!parsed.success) {
        return;
      }

      const tick = parsed.data;
      counter += 1;

      const delayMs = calculateClientDelayMs(tick.timestamp, Date.now());
      secondDelays.push(delayMs);
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
  }, [connection, connectionState, paused]);

  useEffect(() => {
    if (!connection || connectionState !== 'Подключено') {
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
  }, [connection, connectionState, exchangeFilter, symbolFilter]);

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

  const performanceChart = useMemo(() => {
    if (!performanceHistory.length) {
      return {
        hasData: false,
        width: 420,
        height: 220,
        minX: 0,
        maxX: 0,
        maxY: 0,
        messagesPath: '',
        delayPath: '',
        labels: [] as PerformancePoint[],
        yTicks: [] as PerformanceYAxisTick[],
        messagesMin: 0,
        messagesMax: 0,
        delayMin: 0,
        delayMax: 0
      };
    }

    const width = Math.max(420, performanceHistory.length * 28);
    const height = 220;
    const minX = 46;
    const maxX = width - 46;
    const minY = 12;
    const maxY = height - 26;
    const plotWidth = Math.max(maxX - minX, 1);
    const plotHeight = Math.max(maxY - minY, 1);
    const messageValues = performanceHistory.map((item) => item.messagesPerSecond);
    const delayValues = performanceHistory.map((item) => item.delayP95Ms);
    const messagesMin = Math.min(...messageValues);
    const messagesMax = Math.max(...messageValues);
    const delayMin = Math.min(...delayValues);
    const delayMax = Math.max(...delayValues);
    const messagesSpan = Math.max(messagesMax - messagesMin, Number.EPSILON);
    const delaySpan = Math.max(delayMax - delayMin, Number.EPSILON);
    const yTicks: PerformanceYAxisTick[] = Array.from({ length: PERFORMANCE_Y_TICKS }, (_, index) => {
      const ratio = index / Math.max(PERFORMANCE_Y_TICKS - 1, 1);
      const y = minY + ratio * plotHeight;
      const messagesValue = messagesMax - ratio * (messagesMax - messagesMin);
      const delayValue = delayMax - ratio * (delayMax - delayMin);

      return {
        key: `y-tick-${index}`,
        y,
        messagesValue,
        delayValue
      };
    });

    const buildPath = (values: number[], min: number, span: number): string =>
      values
        .map((value, index) => {
          const x = minX + (plotWidth * index) / Math.max(values.length - 1, 1);
          const y = minY + ((min + span - value) / span) * plotHeight;
          return `${index === 0 ? 'M' : 'L'} ${x.toFixed(2)} ${y.toFixed(2)}`;
        })
        .join(' ');

    return {
      hasData: true,
      width,
      height,
      minX,
      maxX,
      maxY,
      messagesPath: buildPath(messageValues, messagesMin, messagesSpan),
      delayPath: buildPath(delayValues, delayMin, delaySpan),
      labels: performanceHistory,
      yTicks,
      messagesMin,
      messagesMax,
      delayMin,
      delayMax
    };
  }, [performanceHistory]);

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
              setPerformanceHistory([]);
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

      <Card title="График потока (messages/s и задержка p95)">
        {performanceChart.hasData ? (
          <div style={{ border: '1px solid #f0f0f0', borderRadius: 8, padding: 12 }}>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                marginBottom: 8,
                fontSize: 12,
                color: '#595959'
              }}
            >
              <span>
                Messages/s min-max: {formatNumber(performanceChart.messagesMin, 0)} - {formatNumber(performanceChart.messagesMax, 0)}
              </span>
              <span>
                Delay p95 min-max, мс: {formatNumber(performanceChart.delayMin, 0)} - {formatNumber(performanceChart.delayMax, 0)}
              </span>
            </div>
            <div style={{ minWidth: 420, overflowX: 'auto' }}>
              <svg width={performanceChart.width} height={performanceChart.height} role="img" aria-label="График потока">
                <line x1={performanceChart.minX} y1={12} x2={performanceChart.minX} y2={performanceChart.maxY} stroke="#d9d9d9" strokeWidth={1} />
                <line x1={performanceChart.maxX} y1={12} x2={performanceChart.maxX} y2={performanceChart.maxY} stroke="#d9d9d9" strokeWidth={1} />
                <line
                  x1={performanceChart.minX}
                  y1={performanceChart.maxY}
                  x2={performanceChart.maxX}
                  y2={performanceChart.maxY}
                  stroke="#d9d9d9"
                  strokeWidth={1}
                />
                {performanceChart.yTicks.map((tick) => (
                  <g key={tick.key}>
                    <line
                      x1={performanceChart.minX}
                      y1={tick.y}
                      x2={performanceChart.maxX}
                      y2={tick.y}
                      stroke="#f0f0f0"
                      strokeWidth={1}
                    />
                    <line x1={performanceChart.minX - 5} y1={tick.y} x2={performanceChart.minX} y2={tick.y} stroke="#bfbfbf" strokeWidth={1} />
                    <line x1={performanceChart.maxX} y1={tick.y} x2={performanceChart.maxX + 5} y2={tick.y} stroke="#bfbfbf" strokeWidth={1} />
                    <text
                      x={performanceChart.minX - 8}
                      y={tick.y + 3}
                      textAnchor="end"
                      fontSize={10}
                      fill="#8c8c8c"
                    >
                      {formatNumber(tick.messagesValue, 0)}
                    </text>
                    <text
                      x={performanceChart.maxX + 8}
                      y={tick.y + 3}
                      textAnchor="start"
                      fontSize={10}
                      fill="#8c8c8c"
                    >
                      {formatNumber(tick.delayValue, 0)}
                    </text>
                  </g>
                ))}
                <path d={performanceChart.messagesPath} fill="none" stroke="#1677ff" strokeWidth={2} />
                <path d={performanceChart.delayPath} fill="none" stroke="#fa8c16" strokeWidth={2} />
                {performanceChart.labels.map((item, index) => {
                  if (index % 10 !== 0 && index !== performanceChart.labels.length - 1) {
                    return null;
                  }

                  const x =
                    performanceChart.minX +
                    ((performanceChart.maxX - performanceChart.minX) * index) / Math.max(performanceChart.labels.length - 1, 1);
                  return (
                    <text key={item.key} x={x} y={performanceChart.height - 4} textAnchor="middle" fontSize={10} fill="#8c8c8c">
                      {item.timeLabel}
                    </text>
                  );
                })}
              </svg>
            </div>
            <Space style={{ marginTop: 8 }}>
              <Typography.Text style={{ color: '#1677ff' }}>Messages/s</Typography.Text>
              <Typography.Text style={{ color: '#fa8c16' }}>Задержка p95, мс</Typography.Text>
            </Space>
          </div>
        ) : (
          <Typography.Text type="secondary">Недостаточно данных для построения графика потока.</Typography.Text>
        )}
      </Card>
    </Space>
  );
}

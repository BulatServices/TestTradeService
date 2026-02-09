import { describe, expect, it } from 'vitest';
import { buildCandles, calculateClientDelayMs, getDeterministicChartSymbol } from './StreamPage';

const baseTs = new Date('2026-02-09T12:00:00Z').getTime();

function tick(symbol: string, price: number, volume: number, offsetMs: number, rowId: string) {
  return {
    rowId,
    source: 'Bybit-WebSocket',
    exchange: 'Bybit',
    symbol,
    price,
    volume,
    timestamp: new Date(baseTs + offsetMs).toISOString(),
    receivedAt: new Date(baseTs + offsetMs + 10).toISOString()
  };
}

describe('StreamPage chart helpers', () => {
  it('считает клиентскую задержку от времени события до момента получения в браузере', () => {
    const tickTimestamp = new Date(baseTs).toISOString();
    expect(calculateClientDelayMs(tickTimestamp, baseTs + 37)).toBe(37);
    expect(calculateClientDelayMs(tickTimestamp, baseTs - 10)).toBe(0);
  });

  it('выбирает детерминированный тикер при отсутствии фильтра', () => {
    const ticks = [
      tick('SOL-USD', 10, 1, 0, '1'),
      tick('btc-usd', 11, 1, 10, '2'),
      tick('ETH-USD', 12, 1, 20, '3')
    ];

    expect(getDeterministicChartSymbol(ticks)).toBe('btc-usd');
  });

  it('строит свечи только по выбранному тикеру', () => {
    const ticks = [
      tick('BTC-USD', 100, 1, 0, '1'),
      tick('BTC-USD', 110, 2, 10, '2'),
      tick('BTC-USD', 90, 3, 20, '3'),
      tick('BTC-USD', 120, 4, 30, '4'),
      tick('BTC-USD', 105, 5, 40, '5'),
      tick('ETH-USD', 300, 6, 50, '6')
    ];

    const chart = buildCandles(ticks, 'BTC-USD');

    expect(chart.symbol).toBe('BTC-USD');
    expect(chart.candles).toHaveLength(1);
    expect(chart.candles[0].open).toBe(100);
    expect(chart.candles[0].high).toBe(120);
    expect(chart.candles[0].low).toBe(90);
    expect(chart.candles[0].close).toBe(105);
    expect(chart.candles[0].volume).toBe(15);
  });
});

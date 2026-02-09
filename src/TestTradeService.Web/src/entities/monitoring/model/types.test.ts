import { describe, expect, it } from 'vitest';
import { monitoringSnapshotSchema } from './types';

describe('monitoringSnapshotSchema', () => {
  it('валидирует расширенный контракт мониторинга', () => {
    const result = monitoringSnapshotSchema.safeParse({
      timestamp: new Date().toISOString(),
      exchangeStats: {
        Bybit: {
          exchange: 'Bybit',
          tickCount: 10,
          aggregateCount: 2,
          averageDelayMs: 30,
          lastTickTime: new Date().toISOString(),
          windowTickCount: 5,
          windowAggregateCount: 1,
          windowAvgDelayMs: 25,
          windowMaxDelayMs: 60,
          windowTickRatePerSec: 0.5,
          windowAggregateRatePerSec: 0.1
        }
      },
      sourceStats: {
        'Bybit-spot': {
          source: 'Bybit-spot',
          tickCount: 10,
          aggregateCount: 2,
          averageDelayMs: 30,
          lastTickTime: new Date().toISOString(),
          status: 'Ok',
          lastTickAgeMs: 120,
          windowTickCount: 5,
          windowAggregateCount: 1,
          windowAvgDelayMs: 25,
          windowMaxDelayMs: 60,
          windowTickRatePerSec: 0.5,
          windowAggregateRatePerSec: 0.1
        }
      },
      performanceReport: {
        windowMinutes: 5,
        totalWindowTickCount: 5,
        totalWindowAggregateCount: 1,
        totalWindowAvgDelayMs: 25,
        totalWindowMaxDelayMs: 60,
        totalWindowTickRatePerSec: 0.5,
        totalWindowAggregateRatePerSec: 0.1,
        sourcesOk: 1,
        sourcesWarn: 0,
        sourcesCritical: 0
      },
      warnings: []
    });

    expect(result.success).toBe(true);
  });

  it('отклоняет контракт без performanceReport', () => {
    const result = monitoringSnapshotSchema.safeParse({
      timestamp: new Date().toISOString(),
      exchangeStats: {},
      sourceStats: {},
      warnings: []
    });

    expect(result.success).toBe(false);
  });
});

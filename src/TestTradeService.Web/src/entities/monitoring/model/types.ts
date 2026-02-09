import { z } from 'zod';

export const exchangeStatsSchema = z.object({
  exchange: z.string(),
  tickCount: z.number().int(),
  aggregateCount: z.number().int(),
  averageDelayMs: z.number(),
  lastTickTime: z.string(),
  windowTickCount: z.number().int(),
  windowAggregateCount: z.number().int(),
  windowAvgDelayMs: z.number(),
  windowMaxDelayMs: z.number(),
  windowTickRatePerSec: z.number(),
  windowAggregateRatePerSec: z.number()
});

export const sourceStatsSchema = z.object({
  source: z.string(),
  tickCount: z.number().int(),
  aggregateCount: z.number().int(),
  averageDelayMs: z.number(),
  lastTickTime: z.string(),
  status: z.enum(['Ok', 'Warn', 'Critical']),
  lastTickAgeMs: z.number(),
  windowTickCount: z.number().int(),
  windowAggregateCount: z.number().int(),
  windowAvgDelayMs: z.number(),
  windowMaxDelayMs: z.number(),
  windowTickRatePerSec: z.number(),
  windowAggregateRatePerSec: z.number()
});

export const performanceReportSchema = z.object({
  windowMinutes: z.number().int(),
  totalWindowTickCount: z.number().int(),
  totalWindowAggregateCount: z.number().int(),
  totalWindowAvgDelayMs: z.number(),
  totalWindowMaxDelayMs: z.number(),
  totalWindowTickRatePerSec: z.number(),
  totalWindowAggregateRatePerSec: z.number(),
  sourcesOk: z.number().int(),
  sourcesWarn: z.number().int(),
  sourcesCritical: z.number().int()
});

export const monitoringSnapshotSchema = z.object({
  timestamp: z.string(),
  exchangeStats: z.record(exchangeStatsSchema),
  sourceStats: z.record(sourceStatsSchema),
  performanceReport: performanceReportSchema,
  warnings: z.array(z.string())
});

export type ExchangeStatsDto = z.infer<typeof exchangeStatsSchema>;
export type SourceStatsDto = z.infer<typeof sourceStatsSchema>;
export type MonitoringSnapshotDto = z.infer<typeof monitoringSnapshotSchema>;


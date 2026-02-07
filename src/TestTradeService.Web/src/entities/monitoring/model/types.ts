import { z } from 'zod';

export const exchangeStatsSchema = z.object({
  exchange: z.string(),
  tickCount: z.number().int(),
  aggregateCount: z.number().int(),
  averageDelayMs: z.number(),
  lastTickTime: z.string()
});

export const sourceStatsSchema = z.object({
  source: z.string(),
  tickCount: z.number().int(),
  aggregateCount: z.number().int(),
  averageDelayMs: z.number(),
  lastTickTime: z.string()
});

export const monitoringSnapshotSchema = z.object({
  timestamp: z.string(),
  exchangeStats: z.record(exchangeStatsSchema),
  sourceStats: z.record(sourceStatsSchema),
  warnings: z.array(z.string())
});

export type ExchangeStatsDto = z.infer<typeof exchangeStatsSchema>;
export type SourceStatsDto = z.infer<typeof sourceStatsSchema>;
export type MonitoringSnapshotDto = z.infer<typeof monitoringSnapshotSchema>;


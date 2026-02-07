import { z } from 'zod';

export const candleSchema = z.object({
  source: z.string(),
  exchange: z.string(),
  symbol: z.string(),
  windowStart: z.string(),
  window: z.string(),
  open: z.number(),
  high: z.number(),
  low: z.number(),
  close: z.number(),
  volume: z.number(),
  count: z.number().int()
});

export const metricsSnapshotSchema = z.object({
  symbol: z.string(),
  windowStart: z.string(),
  window: z.string(),
  averagePrice: z.number(),
  volatility: z.number(),
  count: z.number().int()
});

export const candlesResponseSchema = z.object({
  total: z.number().int().nonnegative(),
  items: z.array(candleSchema)
});

export const metricsResponseSchema = z.object({
  items: z.array(metricsSnapshotSchema)
});

export type CandleDto = z.infer<typeof candleSchema>;
export type MetricsSnapshotDto = z.infer<typeof metricsSnapshotSchema>;
export type CandlesResponseDto = z.infer<typeof candlesResponseSchema>;
export type MetricsResponseDto = z.infer<typeof metricsResponseSchema>;


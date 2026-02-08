import { z } from 'zod';

export const tickEventSchema = z.object({
  id: z.string().optional(),
  source: z.string(),
  exchange: z.string(),
  symbol: z.string(),
  price: z.number(),
  volume: z.number(),
  timestamp: z.string(),
  receivedAt: z.string()
});

export const aggregateEventSchema = z.object({
  source: z.string(),
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

export type TickEventDto = z.infer<typeof tickEventSchema>;
export type AggregateEventDto = z.infer<typeof aggregateEventSchema>;


import { z } from 'zod';

export const marketInstrumentProfileSchema = z.object({
  exchange: z.string(),
  marketType: z.string(),
  transport: z.string(),
  symbols: z.array(z.string()),
  targetUpdateIntervalMs: z.number().int().positive(),
  isEnabled: z.boolean()
});

export const sourceConfigSchema = z.object({
  profiles: z.array(marketInstrumentProfileSchema)
});

export type MarketInstrumentProfileDto = z.infer<typeof marketInstrumentProfileSchema>;
export type SourceConfigDto = z.infer<typeof sourceConfigSchema>;


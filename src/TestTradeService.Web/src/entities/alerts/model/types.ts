import { z } from 'zod';

export const alertSchema = z.object({
  rule: z.string(),
  source: z.string(),
  symbol: z.string(),
  message: z.string(),
  timestamp: z.string()
});

export const alertRuleConfigSchema = z.object({
  ruleName: z.string(),
  enabled: z.boolean(),
  exchange: z.string().nullable(),
  symbol: z.string().nullable(),
  parameters: z.record(z.string())
});

export const alertsResponseSchema = z.object({
  items: z.array(alertSchema)
});

export const alertRulesResponseSchema = z.object({
  items: z.array(alertRuleConfigSchema)
});

export type AlertDto = z.infer<typeof alertSchema>;
export type AlertRuleConfigDto = z.infer<typeof alertRuleConfigSchema>;
export type AlertsResponseDto = z.infer<typeof alertsResponseSchema>;
export type AlertRulesResponseDto = z.infer<typeof alertRulesResponseSchema>;


import { describe, expect, it } from 'vitest';
import { alertRulesResponseSchema } from './types';

describe('alertRulesResponseSchema', () => {
  it('валидирует контракт с globalChannels', () => {
    const result = alertRulesResponseSchema.safeParse({
      items: [
        {
          ruleName: 'PriceThreshold',
          enabled: true,
          exchange: null,
          symbol: null,
          parameters: {
            min_price: '100',
            max_price: '200'
          }
        }
      ],
      globalChannels: ['Console', 'File']
    });

    expect(result.success).toBe(true);
  });

  it('отклоняет контракт без globalChannels', () => {
    const result = alertRulesResponseSchema.safeParse({
      items: []
    });

    expect(result.success).toBe(false);
  });
});

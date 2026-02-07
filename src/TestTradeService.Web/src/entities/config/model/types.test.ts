import { describe, expect, it } from 'vitest';
import { sourceConfigSchema } from './types';

describe('sourceConfigSchema', () => {
  it('валидирует корректный контракт конфигурации', () => {
    const result = sourceConfigSchema.safeParse({
      profiles: [
        {
          exchange: 'Kraken',
          marketType: 'Spot',
          transport: 'WebSocket',
          symbols: ['BTC-USD'],
          targetUpdateIntervalMs: 1000,
          isEnabled: true
        }
      ]
    });

    expect(result.success).toBe(true);
  });

  it('отклоняет конфигурацию с пустым профилем', () => {
    const result = sourceConfigSchema.safeParse({ profiles: [{}] });
    expect(result.success).toBe(false);
  });
});


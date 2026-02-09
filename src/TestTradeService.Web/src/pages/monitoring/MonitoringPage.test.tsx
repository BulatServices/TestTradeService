import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MonitoringPage } from './MonitoringPage';
import { getMonitoringSnapshot } from '../../features/monitoring/api/monitoringApi';
import { getAlerts, getAlertRules, putAlertRules } from '../../features/alerts/api/alertsApi';
import { getSourceConfig } from '../../features/config/api/configApi';

vi.mock('../../features/monitoring/api/monitoringApi', () => ({
  getMonitoringSnapshot: vi.fn()
}));

vi.mock('../../features/alerts/api/alertsApi', () => ({
  getAlerts: vi.fn(),
  getAlertRules: vi.fn(),
  putAlertRules: vi.fn()
}));

vi.mock('../../features/config/api/configApi', () => ({
  getSourceConfig: vi.fn()
}));

describe('MonitoringPage', () => {
  it('отправляет globalChannels вместе с правилами при сохранении', async () => {
    vi.mocked(getMonitoringSnapshot).mockResolvedValue({
      timestamp: new Date().toISOString(),
      exchangeStats: {},
      sourceStats: {},
      performanceReport: {
        windowMinutes: 5,
        totalWindowTickCount: 0,
        totalWindowAggregateCount: 0,
        totalWindowAvgDelayMs: 0,
        totalWindowMaxDelayMs: 0,
        totalWindowTickRatePerSec: 0,
        totalWindowAggregateRatePerSec: 0,
        sourcesOk: 0,
        sourcesWarn: 0,
        sourcesCritical: 0
      },
      warnings: []
    });
    vi.mocked(getAlerts).mockResolvedValue({ items: [] });
    vi.mocked(getSourceConfig).mockResolvedValue({
      profiles: [
        {
          exchange: 'Kraken',
          marketType: 'Spot',
          transport: 'WebSocket',
          symbols: ['XBT/USD'],
          targetUpdateIntervalMs: 2000,
          isEnabled: true
        }
      ]
    });
    vi.mocked(getAlertRules).mockResolvedValue({
      items: [
        {
          ruleName: 'PriceThreshold',
          enabled: true,
          exchange: null,
          symbol: null,
          parameters: {
            min_price: '18000',
            max_price: '22000',
            channels: 'File'
          }
        }
      ],
      globalChannels: ['Console', 'EmailStub']
    });
    vi.mocked(putAlertRules).mockResolvedValue({
      items: [],
      globalChannels: ['Console', 'EmailStub']
    });

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false
        },
        mutations: {
          retry: false
        }
      }
    });

    render(
      <QueryClientProvider client={queryClient}>
        <MonitoringPage />
      </QueryClientProvider>
    );

    const saveButton = await screen.findByRole('button', { name: 'Сохранить правила' });
    await userEvent.click(saveButton);

    await waitFor(() => {
      expect(putAlertRules).toHaveBeenCalled();
    });

    expect(putAlertRules).toHaveBeenCalledWith({
      items: [
        {
          ruleName: 'PriceThreshold',
          enabled: true,
          exchange: 'Kraken',
          symbol: 'XBT/USD',
          parameters: {
            min_price: '18000',
            max_price: '22000',
            channels: 'File'
          }
        }
      ],
      globalChannels: ['Console', 'EmailStub']
    });
  });
});

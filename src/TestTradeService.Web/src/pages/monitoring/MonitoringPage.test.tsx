import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MonitoringPage } from './MonitoringPage';
import { getMonitoringSnapshot } from '../../features/monitoring/api/monitoringApi';
import { getAlerts, getAlertRules, putAlertRules } from '../../features/alerts/api/alertsApi';

vi.mock('../../features/monitoring/api/monitoringApi', () => ({
  getMonitoringSnapshot: vi.fn()
}));

vi.mock('../../features/alerts/api/alertsApi', () => ({
  getAlerts: vi.fn(),
  getAlertRules: vi.fn(),
  putAlertRules: vi.fn()
}));

describe('MonitoringPage', () => {
  it('отправляет globalChannels вместе с правилами при сохранении', async () => {
    vi.mocked(getMonitoringSnapshot).mockResolvedValue({
      timestamp: new Date().toISOString(),
      exchangeStats: {},
      sourceStats: {},
      warnings: []
    });
    vi.mocked(getAlerts).mockResolvedValue({ items: [] });
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
  });
});

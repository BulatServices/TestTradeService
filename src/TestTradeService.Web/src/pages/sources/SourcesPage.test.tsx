import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getSourceConfig, putSourceConfig } from '../../features/config/api/configApi';
import { SourcesPage } from './SourcesPage';

vi.mock('../../features/config/api/configApi', () => ({
  getSourceConfig: vi.fn(),
  putSourceConfig: vi.fn()
}));

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  });
}

describe('SourcesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('отправляет сохранение при нажатии "Применить изменения"', async () => {
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
    vi.mocked(putSourceConfig).mockResolvedValue({
      profiles: [
        {
          exchange: 'Kraken',
          marketType: 'Spot',
          transport: 'WebSocket',
          symbols: ['BTC-USD'],
          targetUpdateIntervalMs: 2000,
          isEnabled: true
        }
      ]
    });

    render(
      <QueryClientProvider client={createQueryClient()}>
        <SourcesPage />
      </QueryClientProvider>
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Изменить' }));
    const symbolsInput = await screen.findByPlaceholderText('BTC-USD, ETH-USD');
    await userEvent.clear(symbolsInput);
    await userEvent.type(symbolsInput, 'BTC-USD');

    await userEvent.click(screen.getByRole('button', { name: 'Применить изменения' }));

    await waitFor(() => {
      expect(putSourceConfig).toHaveBeenCalledTimes(1);
    });

    expect(putSourceConfig).toHaveBeenCalledWith({
      profiles: [
        {
          exchange: 'Kraken',
          marketType: 'Spot',
          transport: 'WebSocket',
          symbols: ['BTC-USD'],
          targetUpdateIntervalMs: 2000,
          isEnabled: true
        }
      ]
    });
  });

  it('отправляет сохранение по кнопке "Сохранить" с несохраненными изменениями формы', async () => {
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
    vi.mocked(putSourceConfig).mockResolvedValue({
      profiles: [
        {
          exchange: 'Kraken',
          marketType: 'Spot',
          transport: 'WebSocket',
          symbols: ['BTC-USD'],
          targetUpdateIntervalMs: 2000,
          isEnabled: true
        }
      ]
    });

    render(
      <QueryClientProvider client={createQueryClient()}>
        <SourcesPage />
      </QueryClientProvider>
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Изменить' }));
    const symbolsInput = await screen.findByPlaceholderText('BTC-USD, ETH-USD');
    await userEvent.clear(symbolsInput);
    await userEvent.type(symbolsInput, 'BTC-USD');

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => {
      expect(putSourceConfig).toHaveBeenCalledTimes(1);
    });

    expect(putSourceConfig).toHaveBeenCalledWith({
      profiles: [
        {
          exchange: 'Kraken',
          marketType: 'Spot',
          transport: 'WebSocket',
          symbols: ['BTC-USD'],
          targetUpdateIntervalMs: 2000,
          isEnabled: true
        }
      ]
    });
  });
});

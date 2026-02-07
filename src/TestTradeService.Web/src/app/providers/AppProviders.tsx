import { PropsWithChildren, useState } from 'react';
import { App as AntApp, ConfigProvider } from 'antd';
import ruRU from 'antd/locale/ru_RU';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MarketHubProvider } from '../../shared/signalr/MarketHubProvider';

export function AppProviders({ children }: PropsWithChildren) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: 1,
            refetchOnWindowFocus: false
          }
        }
      })
  );

  return (
    <ConfigProvider locale={ruRU}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <MarketHubProvider>{children}</MarketHubProvider>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  );
}


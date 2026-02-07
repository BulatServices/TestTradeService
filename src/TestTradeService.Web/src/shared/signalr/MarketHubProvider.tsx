import {
  PropsWithChildren,
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState
} from 'react';
import { HubConnection } from '@microsoft/signalr';
import { createMarketHubConnection, mapConnectionState } from './marketHub';
import { HubConnectionState } from './types';

interface MarketHubContextValue {
  connectionState: HubConnectionState;
  reconnectCount: number;
  connection: HubConnection | null;
}

const MarketHubContext = createContext<MarketHubContextValue>({
  connectionState: 'Отключено',
  reconnectCount: 0,
  connection: null
});

export function MarketHubProvider({ children }: PropsWithChildren) {
  const [connectionState, setConnectionState] = useState<HubConnectionState>('Отключено');
  const [reconnectCount, setReconnectCount] = useState(0);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = createMarketHubConnection();
    connectionRef.current = connection;

    connection.onreconnecting(() => {
      setConnectionState('Переподключение');
      setReconnectCount((prev) => prev + 1);
    });

    connection.onreconnected(() => {
      setConnectionState('Подключено');
    });

    connection.onclose(() => {
      setConnectionState('Отключено');
    });

    const start = async () => {
      try {
        await connection.start();
        setConnectionState(mapConnectionState(connection.state));
      } catch (error) {
        console.warn('Не удалось подключиться к SignalR hub', error);
        setConnectionState('Отключено');
      }
    };

    void start();

    return () => {
      void connection.stop();
    };
  }, []);

  const value = useMemo(
    () => ({
      connectionState,
      reconnectCount,
      connection: connectionRef.current
    }),
    [connectionState, reconnectCount]
  );

  return <MarketHubContext.Provider value={value}>{children}</MarketHubContext.Provider>;
}

export function useMarketHubContext() {
  return useContext(MarketHubContext);
}


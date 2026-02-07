import { Tag } from 'antd';
import { useMarketHubContext } from '../signalr/MarketHubProvider';

export function ConnectionBadge() {
  const { connectionState } = useMarketHubContext();

  const color =
    connectionState === 'Подключено'
      ? 'success'
      : connectionState === 'Переподключение'
        ? 'warning'
        : 'error';

  return <Tag color={color}>SignalR: {connectionState}</Tag>;
}


import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { env } from '../config/env';

export function createMarketHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(env.signalRUrl)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export function mapConnectionState(state: HubConnectionState): 'Подключено' | 'Переподключение' | 'Отключено' {
  if (state === HubConnectionState.Connected) {
    return 'Подключено';
  }

  if (state === HubConnectionState.Reconnecting) {
    return 'Переподключение';
  }

  return 'Отключено';
}


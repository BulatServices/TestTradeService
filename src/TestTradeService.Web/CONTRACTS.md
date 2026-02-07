# Контракты интеграции фронтенда

## REST
- `GET /api/v1/config/sources`
- `PUT /api/v1/config/sources`
- `GET /api/v1/processed/candles`
- `GET /api/v1/processed/metrics`
- `GET /api/v1/monitoring/snapshot`
- `GET /api/v1/alerts`
- `GET /api/v1/alerts/rules`
- `PUT /api/v1/alerts/rules`

## SignalR
- Hub URL: `/hubs/market-data`
- Серверные события: `tick`, `aggregate`, `monitoring`, `alert`
- Клиентские команды: `SubscribeSymbols`, `UnsubscribeSymbols`, `SetStreamFilter`

## Формат ошибок API
```json
{
  "code": "string",
  "message": "Русское описание ошибки",
  "details": "optional",
  "traceId": "optional"
}
```

## Runtime-валидация
Входящие payload валидируются через `zod`. При несовпадении схемы фронтенд показывает ошибку:
`Несовместимый формат данных от сервера`.

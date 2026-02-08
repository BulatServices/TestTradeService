# TestTradeService

Высокопроизводительная система агрегации и обработки торговых данных на .NET 8.

## План задач (по порядку выполнения)
1. Подготовка инфраструктуры проекта: структура каталогов, базовый хостинг, DI и конфигурация.
2. Реализация источников данных (REST polling, WebSocket stream) через единый интерфейс.
3. Реализация конвейера обработки: нормализация, фильтрация, дедупликация, расчет метрик.
4. Реализация агрегации по временным окнам (1м/5м/1ч) и метрик (средняя цена, волатильность).
5. Реализация слоя хранения: raw тики, агрегаты, метаданные, статусы источников.
6. Реализация алертинга и каналов уведомлений (консоль, файл, email-заглушка).
7. Реализация мониторинга, статистики и репортинга задержек.
8. Тестирование нагрузки, корректности остановки и расширяемости.

## Архитектура
- `Interfaces`: контракты источников, хранения, мониторинга и алертинга.
- `Models`: доменные модели тиков, агрегатов, алертов и метрик.
- `Services`: источники данных, конвейер, агрегация, алертинг.
- `Storage`: реализация хранения (in-memory, может быть заменена на БД).
- `Monitoring`: мониторинг производительности и задержек.

## Запуск
```bash
cd src/TestTradeService

dotnet restore

dotnet run
```

## Примечания по масштабируемости
- Используется `Channel` для неблокирующей очереди и обработки потоков.
- Источники данных работают параллельно, конвейер обработки независим от источников.
- Реализацию `IStorage` можно заменить на PostgreSQL/TimescaleDB или другую БД.
- Метрики и алерты масштабируются через DI и независимые правила.

## Веб-интерфейс (React)

Frontend MVP расположен в `src/TestTradeService.Web`.

Запуск:
```bash
cd src/TestTradeService.Web
npm install
npm run dev
```

Проверки:
```bash
npm run typecheck
npm run lint
npm run test
npm run build
```

Переменные окружения:
- `VITE_API_BASE_URL`
- `VITE_SIGNALR_URL`
- `VITE_REQUEST_TIMEOUT_MS`

## Docker / Docker Compose

Запуск всех контейнеров:
```bash
docker compose up --build -d
```

Остановка:
```bash
docker compose down
```

Сервисы из `docker-compose.yml`:
- `db` — TimescaleDB/PostgreSQL (`localhost:5432`)
- `backend` — .NET worker (`TestTradeService`)
- `web` — собранный frontend, раздаётся через Nginx (`http://localhost:5173`)

Полезные команды:
```bash
docker compose logs -f backend
docker compose logs -f db
docker compose logs -f web
```

Важно: текущий проект `src/TestTradeService` запускается как background worker и не поднимает HTTP API/SignalR endpoint на `:5000`. Поэтому frontend в контейнере собирается с существующими URL-ами из конфигурации, но для полноценной работы API-страниц нужен отдельный backend с HTTP endpoint-ами.

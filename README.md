# Ground Control

Ground Control — компонент системы аэропорта, отвечающий за управление маршрутами и занятостью рёбер графа аэродрома.

## Быстрый старт

```bash
# Локальная разработка (с hot reload)
make dev

# Запустить все тесты
make test

# Продакшен режим (полная система в Docker)
make prod
```

Подробнее: [MAKEFILE_GUIDE.md](MAKEFILE_GUIDE.md) - полное руководство по всем командам

## Описание

Ground Control управляет:
- Графом аэродрома (узлы и рёбра)
- Маршрутами транспортных средств (самолёты и автобусы)
- Занятостью рёбер графа
- TTL маршрутов на основе симуляционного времени

### Основные возможности

- **Резервирование маршрутов**: Построение пути от узла к узлу с проверкой занятости рёбер
- **Освобождение маршрутов**: Освобождение занятых рёбер при завершении движения
- **TTL управление**: Автоматическое истечение маршрутов при паузе симуляции
- **Идемпотентность**: Все операции идемпотентны по `reservationId`/`eventId`
- **Kafka интеграция**: Обработка событий `sim.time.tick` для управления TTL

## Архитектура

Проект использует чистую архитектуру с разделением на слои:

```
Ground_Control/
├── src/
│   ├── GroundControl.Api/          # REST API контроллеры
│   ├── GroundControl.Core/         # Бизнес-логика и интерфейсы
│   └── GroundControl.Infrastructure/ # Реализация БД и Kafka
├── tests/
│   └── GroundControl.Tests/        # Unit тесты
├── Docs/                           # Документация
├── Dockerfile                      # Docker образ
├── docker-compose.yml              # Локальный запуск
└── Makefile                        # Команды сборки
```

### Технологии

- **.NET 8.0** - основной фреймворк
- **PostgreSQL 16** - база данных
- **Entity Framework Core** - ORM
- **Kafka** - обработка событий симуляции
- **Serilog** - логирование
- **xUnit, Moq, FluentAssertions** - тестирование

## API Endpoints

### Health
- `GET /health` - проверка здоровья сервиса
- `GET /version` - версия сервиса

### Graph
- `GET /v1/graph/nodes` - получить все узлы графа
- `GET /v1/graph/edges` - получить все рёбра графа

### Routes
- `GET /v1/routes` - получить маршруты (фильтры: `vehicleId`, `vehicleType`)
- `POST /v1/routes/reserve` - зарезервировать маршрут
- `POST /v1/routes/{routeId}/release` - освободить маршрут

### Occupancy
- `GET /v1/occupancy` - получить текущую занятость рёбер

Полная спецификация API: [`Docs/ground_control.yml`](Docs/ground_control.yml)

## Как поднять блок

### Требования

- Docker и Docker Compose
- .NET 8.0 SDK (для локальной разработки)
- Make (опционально, для удобства)

### Быстрый старт с Docker

```bash
# Сборка образов
make build
# или
docker-compose build

# Запуск сервисов
make run
# или
docker-compose up -d

# Просмотр логов
make logs
# или
docker-compose logs -f ground
```

Сервис будет доступен по адресу:
- **HTTP**: `http://localhost:8012`
- **Swagger UI**: `http://localhost:8012/swagger` (в dev режиме)

### Локальная разработка

```bash
# Восстановить зависимости
dotnet restore

# Запустить только БД
docker-compose up -d ground_db

# Запустить API локально
cd src/GroundControl.Api
dotnet run

# Запустить тесты
dotnet test
# или
make test
```

### Переменные окружения

Настройки в [`appsettings.json`](src/GroundControl.Api/appsettings.json):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=ground_db;Port=5432;Database=ground;Username=postgres;Password=postgres"
  },
  "Kafka": {
    "Enabled": true,
    "BootstrapServers": "kafka:9092",
    "GroupId": "ground-service"
  }
}
```

Можно переопределить через переменные окружения:
- `ConnectionStrings__DefaultConnection`
- `Kafka__Enabled`
- `Kafka__BootstrapServers`

## Как работает блок

### 1. Резервирование маршрута

Когда транспортное средство (самолёт или автобус) запрашивает маршрут:

1. **Проверка идемпотентности**: Если маршрут с `reservationId` уже существует, возвращается существующий
2. **Построение пути**: Используется алгоритм Дейкстры для поиска кратчайшего пути
3. **Проверка занятости**: Проверяются все рёбра пути на занятость
4. **Резервирование**: Если путь свободен, все рёбра помечаются как занятые
5. **Установка TTL**: Маршруту назначается TTL в минутах симуляции

**Пример запроса:**
```bash
curl -X POST http://localhost:8012/v1/routes/reserve \
  -H "Content-Type: application/json" \
  -d '{
    "reservationId": "550e8400-e29b-41d4-a716-446655440000",
    "vehicleId": "PL-1",
    "vehicleType": "plane",
    "fromNode": "P-1",
    "toNode": "RW-1",
    "ttlMinutes": 10
  }'
```

### 2. Освобождение маршрута

Когда транспорт завершает движение:

1. Все рёбра маршрута освобождаются
2. Статус маршрута меняется на `finished`

**Пример запроса:**
```bash
curl -X POST http://localhost:8012/v1/routes/{routeId}/release
```

### 3. TTL и симуляционное время

Ground слушает Kafka топик `sim.events` для событий `sim.time.tick`:

1. При каждом тике TTL всех активных маршрутов уменьшается на `tickMinutes`
2. Если TTL <= 0, маршрут автоматически освобождается
3. События дедуплицируются по `eventId` в таблице `processed_events`

**Важно**: TTL работает по симуляционному времени, а не по реальному. При паузе симуляции TTL не убывает.

### 4. Алгоритм поиска пути

Используется алгоритм Дейкстры:
- Граф строится из таблицы `edges`
- Вес ребра = `length`
- Возвращается список рёбер от начального узла до конечного

### 5. База данных

Схема БД ([`Docs/ground_control_db.sql`](Docs/ground_control_db.sql)):

- **nodes**: Узлы графа (стоянки, взлётные полосы, точки)
- **edges**: Рёбра графа (дороги между узлами)
- **routes**: Зарезервированные маршруты
- **edge_occupancy**: Текущая занятость рёбер
- **processed_events**: Обработанные события Kafka (для идемпотентности)

## Интеграция с другими сервисами

### Входящие запросы

- **Plane/Bus** → `POST /v1/routes/reserve` - запрос маршрута
- **Plane/Bus** → `POST /v1/routes/{routeId}/release` - освобождение маршрута

### Kafka события

**Подписки:**
- `sim.events` → `sim.time.tick` - обновление TTL маршрутов

**Публикации (опционально):**
- `ground.events` → `ground.route.allocated` - маршрут зарезервирован
- `ground.events` → `ground.route.released` - маршрут освобождён
- `ground.events` → `ground.edge.occupied` - ребро занято
- `ground.events` → `ground.edge.freed` - ребро освобождено

## Тестирование

```bash
# Запустить все тесты
dotnet test

# Запустить с покрытием
dotnet test /p:CollectCoverage=true

# Запустить конкретный тест
dotnet test --filter "FullyQualifiedName~PathfinderTests"
```

Тесты включают:
- **PathfinderTests**: Тестирование алгоритма поиска пути
- **RouteServiceTests**: Тестирование бизнес-логики маршрутов

## Остановка и очистка

```bash
# Остановить сервисы
make stop
# или
docker-compose down

# Удалить контейнеры и volumes
make clean
# или
docker-compose down -v
```

## Troubleshooting

### База данных не поднимается

```bash
# Проверить логи БД
make db-logs
# или
docker-compose logs ground_db

# Пересоздать volume
docker-compose down -v
docker-compose up -d ground_db
```

### Kafka не подключается

Убедитесь, что Kafka запущена и доступна по адресу `kafka:9092`. Можно временно отключить Kafka:

```json
{
  "Kafka": {
    "Enabled": false
  }
}
```

### Порт 8012 занят

Измените порт в [`docker-compose.yml`](docker-compose.yml):

```yaml
ports:
  - "8013:8000"  # Изменить 8012 на другой порт
```

## Контакты

- **Автор**: Ворогушин М.
- **Научный руководитель**: Николай Сергеевич
- **Проект**: Распределённая система аэропорта

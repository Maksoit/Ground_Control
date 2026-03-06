# Ground Control — Диспетчер Движения

Микросервис наземного управления аэропорта. Управляет движением воздушных судов и
наземного транспорта по территории аэродрома: хранит граф аэродрома, выдаёт разрешения
на маршрут, следит за занятостью рёбер и TTL маршрутов.

---

## КАК ПОДНИМАТЬ МОИ БЛОКИ

### Быстрый старт (Docker Compose)

```bash
# Собрать и запустить сервис + PostgreSQL 16
make build
make up

# Проверить работу
curl http://localhost:8012/health

# Остановить
make down
```

### С подключением к Kafka

```bash
KAFKA_BOOTSTRAP_SERVERS=kafka:9092 docker compose up -d
```

### Запуск тестов

```bash
make test
# или напрямую
dotnet test tests/GroundControl.Tests/GroundControl.Tests.csproj
```

### Параметры сервиса

| Параметр | Значение |
|---|---|
| Внутренний порт | `8000` |
| Внешний порт | `8012` |
| Nginx prefix | `/api/ground` |
| БД | `ground_db` (PostgreSQL 16) |
| Порт БД | `5442:5432` |

### Переменные окружения

| Переменная | По умолчанию | Описание |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | см. docker-compose.yml | Строка подключения к PostgreSQL |
| `Kafka__BootstrapServers` | _(пусто — Kafka отключена)_ | Адрес Kafka-брокера |

---

## КАК РАБОТАЕТ МОЙ БЛОК

### Концепция

Аэродром представлен как **ориентированный граф**: узлы (`nodes`) — точки (парковки,
пересечения, ВПП), рёбра (`edges`) — дорожки между ними. Для двусторонних дорог
создаются два ребра (A→B и B→A), чтобы занятость учитывалась по направлению.

### Правила

- Разрешение (`reserve`) выдаётся **на весь маршрут** сразу — не по шагам.
- Разрешение запрашивают только `plane` и `bus`; техника типа refuel/catering
  телепортируется к стоянке без рулёжки.
- Занятость считается **на ребре**; узлы не блокируются.
- Маршрут имеет **TTL**; при истечении рёбра освобождаются автоматически (через
  тики симуляционного времени `sim.time.tick` из Kafka или fallback-воркер).

### Компоненты

| Компонент | Описание |
|---|---|
| `PathfinderService` | Dijkstra по рёбрам графа |
| `RouteService` | Бизнес-логика reserve/release/TTL |
| `SimTimeTickWorker` | Kafka consumer `sim.time.tick` → TTL expiry |
| `KafkaProducer` | Публикация `ground.route.allocated`, `ground.route.released` |
| `DbSeeder` | Начальный граф аэродрома при первом запуске |

### REST API

| Метод | URL | Описание |
|---|---|---|
| GET | `/health` | Health check |
| GET | `/v1/graph/nodes` | Список узлов графа |
| GET | `/v1/graph/edges` | Список рёбер графа |
| GET | `/v1/routes` | Маршруты (фильтр: `vehicleId`, `vehicleType`) |
| POST | `/v1/routes/reserve` | Зарезервировать маршрут |
| POST | `/v1/routes/{routeId}/release` | Освободить маршрут |
| GET | `/v1/occupancy` | Занятость рёбер |

#### Пример: зарезервировать маршрут

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

#### Пример: освободить маршрут

```bash
curl -X POST http://localhost:8012/v1/routes/550e8400-e29b-41d4-a716-446655440000/release
```

### Kafka

| Топик | Когда публикуется |
|---|---|
| `ground.route.allocated` | Маршрут успешно зарезервирован |
| `ground.route.released` | Маршрут освобождён (вручную или по TTL) |

Ground также слушает топик `sim.time.tick` для уменьшения TTL согласно симуляционному
времени (пауза симуляции не расходует TTL).

### База данных

PostgreSQL 16, контейнер `ground_db`. Схема создаётся автоматически при старте
через EF Core Migrations.

| Таблица | Описание |
|---|---|
| `nodes` | Узлы графа аэродрома |
| `edges` | Рёбра графа аэродрома |
| `routes` | Маршруты (allocated / finished) |
| `edge_occupancy` | Текущая занятость рёбер |
| `processed_events` | Дедупликация Kafka-событий |


# E2E тесты с реальной Kafka

Этот документ описывает end-to-end тестирование Ground Control с реальной Kafka.

## Что тестируется

E2E тесты проверяют полный цикл работы Ground Control в условиях, максимально приближенных к продакшену:

1. Запуск Kafka, PostgreSQL и Ground Control в Docker
2. Резервирование маршрута через REST API
3. Отправка событий `sim.time.tick` в Kafka
4. Автоматическое уменьшение TTL маршрутов
5. Автоматическое истечение маршрутов при превышении TTL
6. Идемпотентность обработки событий

## Архитектура тестового окружения

```
┌─────────────────┐
│  E2E Test Script│
│  (bash)         │
└────────┬────────┘
         │
         ├──────────────────┐
         │                  │
         ▼                  ▼
┌─────────────────┐  ┌──────────────┐
│  Ground Control │  │    Kafka     │
│  (Docker)       │  │  (Docker)    │
└────────┬────────┘  └──────┬───────┘
         │                  │
         ▼                  │
┌─────────────────┐         │
│   PostgreSQL    │◄────────┘
│   (Docker)      │
└─────────────────┘
```

## Запуск E2E тестов

### Через Makefile (рекомендуется)

```bash
make test-e2e
```

### Напрямую

```bash
bash tests/e2e_kafka_test.sh
```

## Что происходит при запуске

### Шаг 1: Запуск окружения
- Поднимается Kafka (порт 9093)
- Поднимается PostgreSQL (порт 5434)
- Поднимается Ground Control (порт 8013)
- Ожидание готовности всех сервисов (30 секунд)

### Шаг 2: Проверка здоровья
- Проверка `/health` endpoint
- Проверка загрузки графа аэропорта

### Шаг 3: Резервирование маршрута
- POST `/v1/routes/reserve` с TTL=5 минут
- Проверка, что маршрут создан
- Проверка, что рёбра заняты

### Шаг 4: Отправка tick события (2 минуты)
- Отправка JSON в Kafka топик `sim.events`
- Ожидание обработки
- Проверка, что маршрут всё ещё активен (TTL=3)

### Шаг 5: Отправка tick события (10 минут)
- Отправка события, превышающего TTL
- Ожидание обработки
- Проверка, что маршрут истёк
- Проверка, что рёбра освобождены

### Шаг 6: Тест идемпотентности
- Отправка одного события дважды
- Проверка, что обработано только один раз

### Шаг 7: Очистка
- Остановка и удаление всех контейнеров

## Требования

- Docker и Docker Compose
- jq (для парсинга JSON)
- curl
- bash

### Установка jq на macOS

```bash
brew install jq
```

## Конфигурация

Тестовое окружение использует отдельные порты:
- Ground Control: 8013 (вместо 8012)
- PostgreSQL: 5434 (вместо 5433)
- Kafka: 9093 (вместо 9092)

Конфигурация в [`docker-compose.test.yml`](../docker-compose.test.yml)

## Формат Kafka событий

События отправляются в топик `sim.events`:

```json
{
  "eventId": "uuid",
  "eventType": "sim.time.tick",
  "tickMinutes": 1,
  "simulationTime": "2024-01-01T12:00:00Z"
}
```

## Troubleshooting

### Проблема: "jq: command not found"

```bash
brew install jq
```

### Проблема: Порты заняты

Измените порты в `docker-compose.test.yml`:
- 8013 → другой порт
- 5434 → другой порт
- 9093 → другой порт

### Проблема: Kafka не запускается

```bash
# Посмотреть логи
docker-compose -f docker-compose.test.yml logs kafka_test

# Увеличить время ожидания в скрипте
# Измените sleep 30 на sleep 60
```

### Проблема: Тесты падают

```bash
# Запустить с логами
docker-compose -f docker-compose.test.yml up

# В другом терминале запустить скрипт
bash tests/e2e_kafka_test.sh

# Посмотреть логи Ground Control
docker-compose -f docker-compose.test.yml logs ground_test
```

## Ручное тестирование

Можно запустить окружение и тестировать вручную:

```bash
# Запустить окружение
docker-compose -f docker-compose.test.yml up -d

# Проверить здоровье
curl http://localhost:8013/health

# Зарезервировать маршрут
curl -X POST http://localhost:8013/v1/routes/reserve \
  -H "Content-Type: application/json" \
  -d '{
    "reservationId": "550e8400-e29b-41d4-a716-446655440000",
    "vehicleId": "PL-1",
    "vehicleType": "plane",
    "fromNode": "T-1",
    "toNode": "RW-1",
    "ttlMinutes": 10
  }'

# Отправить Kafka событие
docker exec kafka_test kafka-console-producer \
  --bootstrap-server localhost:9093 \
  --topic sim.events << EOF
{
  "eventId": "$(uuidgen)",
  "eventType": "sim.time.tick",
  "tickMinutes": 3,
  "simulationTime": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
}
EOF

# Проверить занятость
curl http://localhost:8013/v1/occupancy

# Остановить
docker-compose -f docker-compose.test.yml down -v
```

## Интеграция в CI/CD

Скрипт можно использовать в CI/CD пайплайнах:

```yaml
# GitHub Actions example
- name: Run E2E tests
  run: make test-e2e
```

## Связанные файлы

- [`docker-compose.test.yml`](../docker-compose.test.yml) - конфигурация тестового окружения
- [`e2e_kafka_test.sh`](e2e_kafka_test.sh) - скрипт E2E тестов
- [`KafkaIntegrationTests.cs`](GroundControl.Tests/KafkaIntegrationTests.cs) - unit тесты Kafka логики
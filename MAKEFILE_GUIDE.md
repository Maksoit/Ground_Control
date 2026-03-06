# Makefile Guide - Ground Control

Руководство по командам Makefile для управления проектом Ground Control.

## Три основных режима работы

### 1. Development Mode (Локальная разработка)

```bash
make dev
```

Что происходит:
1. Запускается PostgreSQL в Docker контейнере
2. API запускается локально с hot reload (автоперезагрузка при изменении кода)
3. Kafka отключен

Когда использовать:
- Разработка новых функций
- Отладка кода
- Быстрое тестирование изменений

Доступ:
- API: http://localhost:8000
- Swagger: http://localhost:8000/swagger
- БД: localhost:5433

Остановка:
```bash
# Ctrl+C для остановки API
make stop  # Остановить БД
```

### 2. Testing Mode (Тестирование)

```bash
# Все тесты (unit + integration)
make test

# Только unit тесты
make test-unit

# Только integration тесты
make test-int

# Тесты с автоперезапуском при изменениях
make test-watch
```

Что тестируется:

Unit тесты:
- PathfinderTests - алгоритм поиска пути (Дейкстра)
- RouteServiceTests - бизнес-логика маршрутов

Integration тесты:
- IntegrationTests - полные сценарии работы с БД
  - Резервирование маршрутов
  - Конфликты путей
  - Освобождение маршрутов
  - Идемпотентность

Когда использовать:
- Перед коммитом изменений
- После добавления новых функций
- Для проверки регрессий

### 3. Production Mode (Продакшен)

```bash
make prod
```

Что происходит:
1. Собираются Docker образы
2. Запускаются все сервисы в контейнерах:
   - PostgreSQL (БД)
   - Ground Control API
3. Автоматически загружается схема аэропорта
4. Kafka отключен (можно включить в docker-compose.yml)

Когда использовать:
- Интеграция с другими модулями системы аэропорта
- Демонстрация работы системы
- Тестирование в условиях, близких к продакшену

Доступ:
- API: http://localhost:8012
- Swagger: http://localhost:8012/swagger
- БД: localhost:5433

Остановка:
```bash
make stop
```

## Все команды Makefile

### Основные команды

| Команда | Описание | Когда использовать |
|---------|----------|-------------------|
| `make help` | Показать справку | Забыли команды |
| `make dev` | Локальная разработка | Разработка и отладка |
| `make test` | Все тесты | Перед коммитом |
| `make prod` | Продакшен режим | Интеграция/демо |

### Тестирование

| Команда | Описание |
|---------|----------|
| `make test` | Все тесты (unit + integration) |
| `make test-unit` | Только unit тесты |
| `make test-int` | Только integration тесты |
| `make test-watch` | Тесты с автоперезапуском |

### Утилиты

| Команда | Описание |
|---------|----------|
| `make stop` | Остановить все сервисы |
| `make clean` | Удалить контейнеры и volumes |
| `make restore` | Восстановить NuGet пакеты |
| `make build-local` | Собрать проект локально |
| `make logs` | Показать логи API |
| `make db-logs` | Показать логи БД |
| `make status` | Статус всех сервисов |
| `make restart` | Перезапустить сервисы |

### База данных

| Команда | Описание |
|---------|----------|
| `make db-shell` | Подключиться к PostgreSQL |
| `make db-reset` | Сбросить БД (удалить все данные) |

## Типичные сценарии использования

### Сценарий 1: Начало работы над новой функцией

```bash
# 1. Запустить в dev режиме
make dev

# 2. Редактировать код в VS Code
# Изменения применяются автоматически (hot reload)

# 3. Тестировать через Swagger
# http://localhost:8000/swagger

# 4. Остановить (Ctrl+C, затем)
make stop
```

### Сценарий 2: Проверка перед коммитом

```bash
# 1. Запустить все тесты
make test

# 2. Если тесты прошли - коммитить
git add .
git commit -m "Add new feature"
```

### Сценарий 3: Демонстрация работы системы

```bash
# 1. Запустить в prod режиме
make prod

# 2. Проверить статус
make status

# 3. Открыть Swagger
# http://localhost:8012/swagger

# 4. Посмотреть логи при необходимости
make logs

# 5. Остановить
make stop
```

### Сценарий 4: Отладка проблем с БД

```bash
# 1. Подключиться к БД
make db-shell

# 2. Выполнить SQL запросы
SELECT * FROM nodes;
SELECT * FROM edges;
SELECT * FROM routes;
\q  # Выход

# 3. Если нужно сбросить БД
make db-reset
```

### Сценарий 5: Интеграция с другими модулями

```bash
# 1. Запустить Ground Control
make prod

# 2. Другие модули могут обращаться к API
# http://localhost:8012/v1/routes/reserve

# 3. Мониторить логи
make logs

# 4. Проверить занятость дорог
curl http://localhost:8012/v1/occupancy
```

## Troubleshooting

### Проблема: "make: command not found"

Решение:
```bash
# Установить make через Homebrew
brew install make
```

### Проблема: Порт 8000 или 8012 занят

Решение:
```bash
# Найти процесс
lsof -i :8000
lsof -i :8012

# Убить процесс
kill -9 <PID>

# Или изменить порт в docker-compose.yml
```

### Проблема: База данных не запускается

Решение:
```bash
# Посмотреть логи
make db-logs

# Сбросить БД
make db-reset

# Очистить всё
make clean
make prod
```

### Проблема: Тесты падают

Решение:
```bash
# Восстановить пакеты
make restore

# Пересобрать
make build-local

# Запустить тесты снова
make test
```

## Структура Makefile

```
Makefile
├── DEVELOPMENT MODE
│   └── make dev          # Локальная разработка
│
├── TESTING
│   ├── make test         # Все тесты
│   ├── make test-unit    # Unit тесты
│   ├── make test-int     # Integration тесты
│   └── make test-watch   # Тесты с hot reload
│
├── PRODUCTION MODE
│   └── make prod         # Продакшен
│
├── UTILITIES
│   ├── make stop         # Остановить
│   ├── make clean        # Очистить
│   ├── make restore      # Восстановить пакеты
│   ├── make logs         # Логи API
│   ├── make db-logs      # Логи БД
│   ├── make status       # Статус сервисов
│   └── make restart      # Перезапустить
│
└── DATABASE UTILITIES
    ├── make db-shell     # Подключиться к БД
    └── make db-reset     # Сбросить БД
```

## Советы

1. Всегда начинайте с `make help` - чтобы вспомнить доступные команды

2. Используйте `make dev` для разработки - hot reload экономит время

3. Запускайте `make test` перед коммитом - избегайте багов

4. Используйте `make prod` для демо - показывает реальную работу системы

5. Смотрите логи при проблемах - `make logs` или `make db-logs`

6. Очищайте периодически - `make clean` освобождает место

## Связанные документы

- [README.md](README.md) - Основная документация
- [QUICKSTART.md](QUICKSTART.md) - Быстрый старт
- [SETUP_MACOS.md](SETUP_MACOS.md) - Установка на macOS
- [test.http](test.http) - Примеры API запросов
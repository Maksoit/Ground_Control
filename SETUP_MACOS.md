# Инструкция по установке и запуску Ground Control на macOS (M3)

Эта инструкция предназначена для запуска проекта на MacBook Air 13 M3 через Visual Studio Code с нуля.

## Шаг 1: Установка необходимого ПО

### 1.1 Установка Homebrew (менеджер пакетов для macOS)

Откройте Terminal (Терминал) и выполните:

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

После установки добавьте Homebrew в PATH (команды появятся в конце установки):

```bash
echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
eval "$(/opt/homebrew/bin/brew shellenv)"
```

### 1.2 Установка .NET 8.0 SDK

```bash
brew install --cask dotnet-sdk
```

Проверьте установку:

```bash
dotnet --version
# Должно показать версию 8.0.x
```

### 1.3 Установка Docker Desktop

```bash
brew install --cask docker
```

После установки:
1. Откройте Docker Desktop из Applications (Программы)
2. Дождитесь запуска Docker (иконка кита в верхней панели станет активной)
3. Примите условия использования

Проверьте установку:

```bash
docker --version
docker-compose --version
```

### 1.4 Установка расширений VS Code

Откройте Visual Studio Code и установите расширения:

1. Откройте VS Code
2. Нажмите `Cmd+Shift+X` (открыть Extensions)
3. Установите следующие расширения:
   - **C# Dev Kit** (ms-dotnettools.csdevkit)
   - **C#** (ms-dotnettools.csharp)
   - **Docker** (ms-azuretools.vscode-docker)
   - **REST Client** (humao.rest-client) - опционально, для тестирования API

Или выполните в терминале VS Code (`Cmd+J`):

```bash
code --install-extension ms-dotnettools.csdevkit
code --install-extension ms-dotnettools.csharp
code --install-extension ms-azuretools.vscode-docker
code --install-extension humao.rest-client
```

## Шаг 2: Открытие проекта в VS Code

1. Откройте VS Code
2. Нажмите `Cmd+O` или File → Open Folder
3. Выберите папку `Ground_Control`
4. VS Code автоматически обнаружит C# проект и предложит установить необходимые зависимости

## Шаг 3: Восстановление зависимостей

В терминале VS Code (`Cmd+J` для открытия терминала):

```bash
dotnet restore
```

Эта команда загрузит все NuGet пакеты, указанные в проекте.

## Шаг 4: Варианты запуска

### Вариант А: Запуск через Docker (рекомендуется для полной системы)

Этот вариант запустит и API, и PostgreSQL базу данных.

#### 4.1 Убедитесь, что Docker Desktop запущен

Проверьте, что иконка Docker в верхней панели активна.

#### 4.2 Запустите сервисы

В терминале VS Code:

```bash
# Сборка образов
docker-compose build

# Запуск сервисов
docker-compose up -d

# Проверка статуса
docker-compose ps
```

#### 4.3 Проверка работы

Откройте браузер и перейдите:
- API: http://localhost:8012/health
- Swagger UI: http://localhost:8012/swagger

#### 4.4 Просмотр логов

```bash
# Логи API
docker-compose logs -f ground

# Логи БД
docker-compose logs -f ground_db
```

#### 4.5 Остановка сервисов

```bash
# Остановить
docker-compose down

# Остановить и удалить данные БД
docker-compose down -v
```

### Вариант Б: Запуск API локально (для разработки)

Этот вариант запускает только API локально, БД в Docker.

#### 4.1 Запустите только базу данных

```bash
docker-compose up -d ground_db
```

#### 4.2 Отключите Kafka в настройках

Откройте файл `src/GroundControl.Api/appsettings.json` и измените:

```json
{
  "Kafka": {
    "Enabled": false
  }
}
```

#### 4.3 Запустите API через VS Code

**Способ 1: Через Debug (F5)**

1. Нажмите `F5` или перейдите в Run and Debug (`Cmd+Shift+D`)
2. Выберите "Launch Ground Control API"
3. Нажмите зелёную кнопку запуска

API запустится на http://localhost:8000

**Способ 2: Через терминал**

```bash
cd src/GroundControl.Api
dotnet run
```

**Способ 3: С автоперезагрузкой (hot reload)**

```bash
cd src/GroundControl.Api
dotnet watch run
```

При изменении кода проект автоматически перезапустится.

#### 4.4 Проверка работы

Откройте браузер:
- API: http://localhost:8000/health
- Swagger UI: http://localhost:8000/swagger

## Шаг 5: Запуск тестов

### Через VS Code

1. Откройте панель Testing (`Cmd+Shift+T`)
2. Нажмите кнопку "Run All Tests" (▶️)

### Через терминал

```bash
# Все тесты
dotnet test

# С подробным выводом
dotnet test --logger "console;verbosity=detailed"

# Конкретный тест
dotnet test --filter "FullyQualifiedName~PathfinderTests"
```

## Шаг 6: Тестирование API

### Через Swagger UI

1. Откройте http://localhost:8012/swagger (Docker) или http://localhost:8000/swagger (локально)
2. Разверните нужный endpoint
3. Нажмите "Try it out"
4. Заполните параметры
5. Нажмите "Execute"

### Через curl

```bash
# Health check
curl http://localhost:8012/health

# Получить узлы графа
curl http://localhost:8012/v1/graph/nodes

# Получить рёбра графа
curl http://localhost:8012/v1/graph/edges

# Зарезервировать маршрут
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

### Через REST Client (расширение VS Code)

Создайте файл `test.http` в корне проекта:

```http
### Health Check
GET http://localhost:8012/health

### Get Nodes
GET http://localhost:8012/v1/graph/nodes

### Get Edges
GET http://localhost:8012/v1/graph/edges

### Reserve Route
POST http://localhost:8012/v1/routes/reserve
Content-Type: application/json

{
  "reservationId": "550e8400-e29b-41d4-a716-446655440000",
  "vehicleId": "PL-1",
  "vehicleType": "plane",
  "fromNode": "P-1",
  "toNode": "RW-1",
  "ttlMinutes": 10
}
```

Нажмите "Send Request" над каждым запросом.

## Шаг 7: Работа с базой данных

### Подключение к PostgreSQL

```bash
# Через Docker
docker exec -it ground_db psql -U postgres -d ground

# Или через psql (если установлен)
psql -h localhost -p 5433 -U postgres -d ground
# Пароль: postgres
```

### Полезные SQL команды

```sql
-- Посмотреть все таблицы
\dt

-- Посмотреть узлы
SELECT * FROM nodes;

-- Посмотреть рёбра
SELECT * FROM edges;

-- Посмотреть маршруты
SELECT * FROM routes;

-- Посмотреть занятость
SELECT * FROM edge_occupancy;

-- Выход
\q
```

## Шаг 8: Полезные команды VS Code

### Задачи (Tasks)

Нажмите `Cmd+Shift+P` и введите "Tasks: Run Task", затем выберите:

- **build** - собрать проект
- **test** - запустить тесты
- **restore** - восстановить зависимости
- **watch** - запустить с hot reload
- **docker-build** - собрать Docker образы
- **docker-up** - запустить Docker контейнеры
- **docker-down** - остановить Docker контейнеры
- **docker-logs** - показать логи

### Горячие клавиши

- `F5` - запустить с отладкой
- `Cmd+Shift+B` - собрать проект
- `Cmd+Shift+T` - открыть панель тестов
- `Cmd+J` - открыть/закрыть терминал
- `Cmd+Shift+P` - командная палитра

## Troubleshooting (Решение проблем)

### Проблема: "dotnet: command not found"

**Решение:**
```bash
# Перезапустите терминал или выполните:
eval "$(/opt/homebrew/bin/brew shellenv)"
```

### Проблема: Docker не запускается

**Решение:**
1. Откройте Docker Desktop из Applications
2. Дождитесь полного запуска (иконка кита станет активной)
3. Если не помогает, перезапустите Docker Desktop

### Проблема: Порт 8012 или 5433 занят

**Решение:**
```bash
# Найти процесс на порту
lsof -i :8012
lsof -i :5433

# Убить процесс (замените PID на реальный)
kill -9 <PID>

# Или измените порты в docker-compose.yml
```

### Проблема: База данных не инициализируется

**Решение:**
```bash
# Удалить volumes и пересоздать
docker-compose down -v
docker-compose up -d ground_db

# Проверить логи
docker-compose logs ground_db
```

### Проблема: Ошибки компиляции в VS Code

**Решение:**
1. Нажмите `Cmd+Shift+P`
2. Введите "OmniSharp: Restart OmniSharp"
3. Или перезапустите VS Code

### Проблема: Тесты не запускаются

**Решение:**
```bash
# Очистить и пересобрать
dotnet clean
dotnet restore
dotnet build
dotnet test
```

## Дополнительные ресурсы

- [Документация .NET](https://learn.microsoft.com/en-us/dotnet/)
- [Документация Docker](https://docs.docker.com/)
- [Документация VS Code C#](https://code.visualstudio.com/docs/languages/csharp)

## Контакты

При возникновении проблем обращайтесь к:
- **Автор**: Ворогушин М.
- **Научный руководитель**: Николай Сергеевич
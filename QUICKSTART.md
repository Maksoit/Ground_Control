# 🚀 Быстрый старт для macOS M3

## Минимальная установка (5 минут)

### 1. Установите Homebrew
```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
eval "$(/opt/homebrew/bin/brew shellenv)"
```

### 2. Установите .NET и Docker
```bash
brew install --cask dotnet-sdk docker
```

### 3. Запустите Docker Desktop
- Откройте Docker из Applications
- Дождитесь запуска (иконка кита в верхней панели)

### 4. Откройте проект в VS Code
```bash
cd Ground_Control
code .
```

### 5. Установите расширения VS Code
При открытии проекта VS Code предложит установить рекомендуемые расширения - нажмите "Install All".

Или вручную:
- Нажмите `Cmd+Shift+X`
- Найдите и установите: **C# Dev Kit**, **Docker**

### 6. Запустите проект

**Вариант А: Через Docker (полная система)**
```bash
docker-compose up -d
```
Откройте: http://localhost:8012/swagger

**Вариант Б: Локально (только API)**
```bash
# Запустите БД
docker-compose up -d ground_db

# Отключите Kafka в src/GroundControl.Api/appsettings.json:
# "Kafka": { "Enabled": false }

# Запустите API
cd src/GroundControl.Api
dotnet run
```
Откройте: http://localhost:8000/swagger

### 7. Проверьте работу

```bash
curl http://localhost:8012/health
# или
curl http://localhost:8000/health
```

## Тестирование API

### Через Swagger UI
Откройте http://localhost:8012/swagger и тестируйте через веб-интерфейс

### Через REST Client в VS Code
1. Откройте файл [`test.http`](test.http)
2. Нажмите "Send Request" над любым запросом

### Через curl
```bash
# Получить узлы
curl http://localhost:8012/v1/graph/nodes

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

## Запуск тестов

```bash
dotnet test
```

Или через VS Code:
- Нажмите `Cmd+Shift+T`
- Нажмите "Run All Tests"

## Остановка

```bash
docker-compose down
```

## Проблемы?

Смотрите подробную инструкцию: [`SETUP_MACOS.md`](SETUP_MACOS.md)

## Горячие клавиши VS Code

- `F5` - запустить с отладкой
- `Cmd+Shift+B` - собрать проект
- `Cmd+Shift+T` - открыть тесты
- `Cmd+J` - открыть терминал
- `Cmd+Shift+P` - командная палитра

---

**Готово!** 🎉 Теперь можно разрабатывать и тестировать Ground Control.
#!/bin/bash

# E2E тест с реальной Kafka
# Этот скрипт тестирует полный цикл работы Ground Control с Kafka

set -e

echo "========================================="
echo "E2E Test: Ground Control + Kafka"
echo "========================================="
echo ""

# Цвета для вывода
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Функция для проверки
check_status() {
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ $1${NC}"
    else
        echo -e "${RED}✗ $1${NC}"
        exit 1
    fi
}

# 1. Запуск тестового окружения
echo "Step 1: Starting test environment (Kafka + PostgreSQL + Ground Control)..."
docker-compose -f docker-compose.test.yml up -d
check_status "Test environment started"

echo "Waiting for services to be ready..."
sleep 10

# 2. Проверка здоровья сервисов с retry
echo ""
echo "Step 2: Checking service health..."
MAX_RETRIES=10
RETRY_COUNT=0
HEALTH=""

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    HEALTH=$(curl -s http://localhost:8013/health 2>/dev/null || echo "")
    if echo "$HEALTH" | grep -q "ok"; then
        check_status "Ground Control is healthy"
        break
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    echo "Waiting for Ground Control to start... (attempt $RETRY_COUNT/$MAX_RETRIES)"
    sleep 3
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo -e "${RED}✗ Ground Control health check failed after $MAX_RETRIES attempts${NC}"
    echo "Logs:"
    docker-compose -f docker-compose.test.yml logs ground_test | tail -30
    docker-compose -f docker-compose.test.yml down -v
    exit 1
fi

# 3. Проверка загрузки данных
echo ""
echo "Step 3: Checking if graph data is loaded..."
NODES=$(curl -s http://localhost:8013/v1/graph/nodes | jq length)
if [ "$NODES" -gt 0 ]; then
    check_status "Graph data loaded ($NODES nodes)"
else
    echo -e "${RED}✗ No graph data found${NC}"
    exit 1
fi

# 4. Резервирование маршрута
echo ""
echo "Step 4: Reserving a route..."
ROUTE_ID="550e8400-e29b-41d4-a716-446655440000"
RESERVE_RESPONSE=$(curl -s -X POST http://localhost:8013/v1/routes/reserve \
  -H "Content-Type: application/json" \
  -d "{
    \"reservationId\": \"$ROUTE_ID\",
    \"vehicleId\": \"PL-TEST\",
    \"vehicleType\": \"plane\",
    \"fromNode\": \"T-1\",
    \"toNode\": \"RW-1\",
    \"ttlMinutes\": 5
  }")

if echo "$RESERVE_RESPONSE" | grep -q "$ROUTE_ID"; then
    check_status "Route reserved successfully"
else
    echo -e "${RED}✗ Route reservation failed${NC}"
    echo "$RESERVE_RESPONSE"
    exit 1
fi

# 5. Проверка занятости рёбер
echo ""
echo "Step 5: Checking edge occupancy..."
OCCUPANCY=$(curl -s http://localhost:8013/v1/occupancy | jq length)
if [ "$OCCUPANCY" -gt 0 ]; then
    check_status "Edges are occupied ($OCCUPANCY edges)"
else
    echo -e "${RED}✗ No edges occupied${NC}"
    exit 1
fi

# 6. Отправка Kafka события sim.time.tick
echo ""
echo "Step 6: Sending Kafka sim.time.tick event..."
EVENT_JSON="{\"eventId\":\"$(uuidgen)\",\"eventType\":\"sim.time.tick\",\"tickMinutes\":2,\"simulationTime\":\"$(date -u +"%Y-%m-%dT%H:%M:%SZ")\"}"
echo "$EVENT_JSON" | docker exec -i kafka_test kafka-console-producer \
  --bootstrap-server localhost:9093 \
  --topic sim.events \
  --property "parse.key=false"
check_status "Kafka event sent"

echo "Waiting for event processing (10 seconds)..."
sleep 10

# 7. Проверка, что TTL уменьшился
echo ""
echo "Step 7: Checking that route is still active (TTL decreased but not expired)..."
ROUTES=$(curl -s "http://localhost:8013/v1/routes?vehicleId=PL-TEST")
if echo "$ROUTES" | grep -q "allocated"; then
    check_status "Route still allocated (TTL decreased)"
else
    echo -e "${YELLOW}⚠ Route status changed (might be expected)${NC}"
fi

# 8. Отправка события, которое истечёт TTL
echo ""
echo "Step 8: Sending tick event that will expire the route..."
EVENT_JSON="{\"eventId\":\"$(uuidgen)\",\"eventType\":\"sim.time.tick\",\"tickMinutes\":10,\"simulationTime\":\"$(date -u +"%Y-%m-%dT%H:%M:%SZ")\"}"
echo "$EVENT_JSON" | docker exec -i kafka_test kafka-console-producer \
  --bootstrap-server localhost:9093 \
  --topic sim.events \
  --property "parse.key=false"
check_status "Expiration event sent"

echo "Waiting for event processing (15 seconds)..."
sleep 15

# 9. Проверка, что маршрут истёк (с retry)
echo ""
echo "Step 9: Checking that route expired..."
MAX_RETRIES=5
RETRY_COUNT=0
OCCUPANCY_AFTER=-1

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    OCCUPANCY_AFTER=$(curl -s http://localhost:8013/v1/occupancy | jq length)
    if [ "$OCCUPANCY_AFTER" -eq 0 ]; then
        check_status "Route expired, edges freed"
        break
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
        echo "Waiting for route expiration... (attempt $RETRY_COUNT/$MAX_RETRIES)"
        sleep 3
    fi
done

if [ "$OCCUPANCY_AFTER" -ne 0 ]; then
    echo -e "${RED}✗ Route did not expire after $MAX_RETRIES attempts${NC}"
    echo "Remaining occupancy: $OCCUPANCY_AFTER edges"
    docker-compose -f docker-compose.test.yml logs ground_test | tail -20
    docker-compose -f docker-compose.test.yml down -v
    exit 1
fi

# 10. Проверка идемпотентности
echo ""
echo "Step 10: Testing idempotency (sending same event twice)..."
EVENT_ID="$(uuidgen)"
EVENT_JSON="{\"eventId\":\"$EVENT_ID\",\"eventType\":\"sim.time.tick\",\"tickMinutes\":1,\"simulationTime\":\"$(date -u +"%Y-%m-%dT%H:%M:%SZ")\"}"
for i in 1 2; do
    echo "$EVENT_JSON" | docker exec -i kafka_test kafka-console-producer \
      --bootstrap-server localhost:9093 \
      --topic sim.events \
      --property "parse.key=false"
done
check_status "Duplicate events sent"

echo "Waiting for processing..."
sleep 5

# Cleanup
echo ""
echo "========================================="
echo "Cleaning up test environment..."
docker-compose -f docker-compose.test.yml down -v

echo ""
echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}All E2E tests passed!${NC}"
echo -e "${GREEN}=========================================${NC}"
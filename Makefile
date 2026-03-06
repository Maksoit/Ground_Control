.PHONY: help dev test test-all prod stop clean restore logs db-logs

help:
	@echo "Ground Control - Makefile Commands"
	@echo ""
	@echo "DEVELOPMENT:"
	@echo "  make dev        - Start for local development (DB + API with hot reload)"
	@echo ""
	@echo "TESTING:"
	@echo "  make test       - Run all tests (unit + integration)"
	@echo "  make test-unit  - Run only unit tests"
	@echo "  make test-int   - Run only integration tests"
	@echo ""
	@echo "PRODUCTION:"
	@echo "  make prod       - Start production environment (Full Docker stack)"
	@echo ""
	@echo "UTILITIES:"
	@echo "  make stop       - Stop all services"
	@echo "  make clean      - Remove containers and volumes"
	@echo "  make restore    - Restore NuGet packages"
	@echo "  make logs       - Show API logs"
	@echo "  make db-logs    - Show database logs"
	@echo "  make status     - Show service status"

# ============================================================
# DEVELOPMENT MODE
# ============================================================
dev:
	@echo "Starting Ground Control in DEVELOPMENT mode..."
	@echo "Starting PostgreSQL database..."
	docker-compose up -d ground_db
	@echo "Waiting for database..."
	@sleep 5
	@echo "Starting API with hot reload..."
	@echo "API: http://localhost:8000"
	@echo "Swagger: http://localhost:8000/swagger"
	@echo ""
	@echo "Press Ctrl+C to stop API, then 'make stop' to stop database"
	@echo ""
	cd src/GroundControl.Api && dotnet watch run

# ============================================================
# TESTING
# ============================================================
test:
	@echo "Running ALL tests (unit + integration)..."
	dotnet test --logger "console;verbosity=normal"

test-unit:
	@echo "Running UNIT tests only..."
	dotnet test --filter "FullyQualifiedName~PathfinderTests|FullyQualifiedName~RouteServiceTests" --logger "console;verbosity=normal"

test-int:
	@echo "Running INTEGRATION tests only..."
	dotnet test --filter "FullyQualifiedName~IntegrationTests" --logger "console;verbosity=normal"

test-watch:
	@echo "Running tests in watch mode..."
	dotnet watch test

# ============================================================
# PRODUCTION MODE
# ============================================================
prod:
	@echo "Starting Ground Control in PRODUCTION mode..."
	@echo "Building Docker images..."
	docker-compose build
	@echo "Starting all services..."
	docker-compose up -d
	@echo "Waiting for services..."
	@sleep 10
	@echo ""
	@echo "Ground Control is running"
	@echo "API: http://localhost:8012"
	@echo "Swagger: http://localhost:8012/swagger"
	@echo "Database: localhost:5433"
	@echo ""
	@echo "Check status: make status"
	@echo "View logs: make logs"
	@echo "Stop: make stop"

# ============================================================
# UTILITIES
# ============================================================
stop:
	@echo "Stopping all services..."
	docker-compose down

clean:
	@echo "Cleaning up containers, volumes, and images..."
	docker-compose down -v
	docker system prune -f

restore:
	@echo "Restoring NuGet packages..."
	dotnet restore

build-local:
	@echo "Building project locally..."
	dotnet build

logs:
	@echo "Showing Ground Control API logs (Ctrl+C to exit)..."
	docker-compose logs -f ground

db-logs:
	@echo "Showing PostgreSQL logs (Ctrl+C to exit)..."
	docker-compose logs -f ground_db

status:
	@echo "Service Status:"
	@docker-compose ps

restart:
	@echo "Restarting services..."
	docker-compose restart

# ============================================================
# DATABASE UTILITIES
# ============================================================
db-shell:
	@echo "Connecting to PostgreSQL..."
	docker exec -it ground_db psql -U postgres -d ground

db-reset:
	@echo "WARNING: Resetting database (all data will be lost)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		docker-compose down -v; \
		docker-compose up -d ground_db; \
		echo "Database reset complete"; \
	else \
		echo "Cancelled"; \
	fi
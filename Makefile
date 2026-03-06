.PHONY: help build run stop clean test restore

help:
	@echo "Ground Control - Makefile commands:"
	@echo "  make build    - Build Docker containers"
	@echo "  make run      - Start all services"
	@echo "  make stop     - Stop all services"
	@echo "  make clean    - Remove containers and volumes"
	@echo "  make test     - Run unit tests"
	@echo "  make restore  - Restore NuGet packages"
	@echo "  make logs     - Show service logs"

build:
	docker-compose build

run:
	docker-compose up -d

stop:
	docker-compose down

clean:
	docker-compose down -v
	docker system prune -f

test:
	dotnet test tests/GroundControl.Tests/GroundControl.Tests.csproj

restore:
	dotnet restore

logs:
	docker-compose logs -f ground

db-logs:
	docker-compose logs -f ground_db
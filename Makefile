.PHONY: build up down restart logs test migrate

build:
	docker compose build

up:
	docker compose up -d

down:
	docker compose down

restart:
	docker compose restart ground

logs:
	docker compose logs -f ground

test:
	dotnet test tests/GroundControl.Tests/GroundControl.Tests.csproj --verbosity normal

migrate:
	dotnet ef migrations add $(name) \
		--project src/GroundControl.Api/GroundControl.Api.csproj \
		--output-dir Data/Migrations

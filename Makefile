# Start everything
up:
	docker compose up -d --build

# Stop everything
down:
	docker compose down

# Restart everything
restart:
	docker compose down
	docker compose up -d --build

# Visit logs
logs:
	docker compose logs -f

# Run EF migrations to create/update users table
migrate:
	cd backend && dotnet ef database update

# Create routing_db, enable extensions, run OSM import, build topology
setup-routing:
	@echo "Creating routing_db..."
	docker exec simplemaps_postgres psql -U ezmapsuser -d postgres -c "CREATE DATABASE routing_db;" 2>/dev/null || echo "routing_db already exists, skipping..."
	@echo "Enabling PostGIS and pgRouting extensions..."
	docker exec simplemaps_postgres psql -U ezmapsuser -d routing_db -c "CREATE EXTENSION IF NOT EXISTS postgis; CREATE EXTENSION IF NOT EXISTS pgrouting;"
	@echo "Running OSM import (this will take several minutes)..."
	docker compose --profile routing-import up osm-routing-import
	@echo "Building routing topology (this will take several minutes)..."
	docker exec -i simplemaps_postgres psql -U ezmapsuser -d routing_db < backend/db/setup_routing.sql
	@echo "Routing setup complete."

# Full fresh setup: bring up stack, migrate, setup routing
bootstrap:
	docker compose up -d --build
	@echo "Waiting for postgres to be ready..."
	sleep 5
	$(MAKE) migrate
	$(MAKE) setup-routing

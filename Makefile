# Restart everything
restart:
	docker compose down
	docker compose up -d --build

# Run EF migrations to create/update users table
migrate:
	cd backend && dotnet ef database update

# Run OSM import then spin up the full stack
bootstrap:
	docker compose up -d --build
	@echo "Waiting for postgres to be ready..."
	until docker exec simplemaps_postgres pg_isready -U ezmapsuser; do sleep 2; done
	@echo "Running EF migrations..."
	$(MAKE) migrate
	@echo "Creating routing_db..."
	docker exec simplemaps_postgres psql -U ezmapsuser -d postgres -c "CREATE DATABASE routing_db;" 2>/dev/null || echo "routing_db already exists, skipping..."
	@echo "Enabling PostGIS and pgRouting extensions..."
	docker exec simplemaps_postgres psql -U ezmapsuser -d routing_db -c "CREATE EXTENSION IF NOT EXISTS postgis; CREATE EXTENSION IF NOT EXISTS pgrouting; CREATE EXTENSION IF NOT EXISTS hstore;"
	@echo "Running OSM import (this will take several minutes)..."
	docker compose --profile routing-import up osm-routing-import
	@echo "Building routing topology (this will take several minutes)..."
	docker exec -i simplemaps_postgres psql -U ezmapsuser -d routing_db < backend/db/setup_routing.sql
	@echo "Done."

# Run k6 load test (requires k6 installed: https://k6.io/docs/get-started/installation)
load-test:
	k6 run testing/load-test.js

# Rebuild routing topology without re-importing OSM data
rebuild-topology:
	@echo "Dropping existing routing tables..."
	docker exec -i simplemaps_postgres psql -U ezmapsuser -d routing_db -c "DROP TABLE IF EXISTS ways CASCADE; DROP TABLE IF EXISTS ways_vertices_pgr CASCADE;"
	@echo "Rebuilding topology..."
	docker exec -i simplemaps_postgres psql -U ezmapsuser -d routing_db < backend/db/setup_routing.sql
	@echo "Topology rebuilt."

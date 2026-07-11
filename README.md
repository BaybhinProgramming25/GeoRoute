# GeoRoute

A self-hosted vehicle routing application built on OpenStreetMap data. Routes are computed using A* pathfinding via pgRouting and PostGIS — no third-party routing APIs.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | Spring Boot (Java 21) |
| Frontend | React + Leaflet |
| Database | PostgreSQL + PostGIS + pgRouting |
| Cache | Redis |
| Data | OpenStreetMap via osm2pgsql |
| Infrastructure | Docker + Docker Compose |
| Load Testing | k6 |

## Features

- A* pathfinding on real OSM road data with per-road-type speed costs
- One-way street support via OSM `oneway` tags
- Turn-by-turn instructions with angle-based turn detection (left, right, keep left, etc.)
- Redis caching with cache locking to prevent thundering herd under concurrent load
- Step markers on the map with hover tooltips showing instructions

## Prerequisites

- Docker + Docker Compose
- An OSM `.pbf` file for your target region placed at `data/region.osm.pbf`

Download OSM data from [Geofabrik](https://download.geofabrik.de/).

## Setup

For a fresh setup run:

```bash
make bootstrap
```

This will:
1. Build and start all containers
2. Create `routing_db` and enable PostGIS, pgRouting, and hstore extensions
3. Import OSM data into PostgreSQL
4. Build the routing topology (creates `ways` and `ways_vertices_pgr` tables)

The OSM import and topology build can take several minutes depending on the size of your region.

## Environment Variables

Create a `backend/.env` file with the following:

```env
POSTGRES_USER=youruser
POSTGRES_PASSWORD=yourpassword
POSTGRES_DB=yourdbname

# Optional overrides (defaults shown)
ROUTING_DB_URL=jdbc:postgresql://postgres:5432/routing_db
REDIS_HOST=redis
REDIS_PORT=6379
```

## Makefile Commands

| Command | Description |
|---|---|
| `make bootstrap` | Full fresh setup — builds images, imports OSM data, builds routing topology |
| `make restart` | Stop and restart all containers with rebuild |
| `make rebuild-topology` | Rebuild routing topology without re-importing OSM data (use after changes to `setup_routing.sql`) |
| `make load-test` | Run k6 load test against the route endpoint (requires k6 installed) |

## Architecture

```
Browser → Nginx (frontend) → React App
                           → /api/* → Spring Boot Backend
                                    → Redis (cache)
                                    → PostgreSQL (routing)
```

### Routing Pipeline

1. User enters start and destination addresses
2. Frontend geocodes them via Nominatim (OSM geocoding API)
3. Coordinates are sent to `POST /api/route`
4. Backend checks Redis cache — cache hit returns instantly
5. On cache miss, a Redis lock is acquired to prevent concurrent duplicate queries
6. `findNearestNode` finds the closest routable road node for each coordinate
7. `pgr_aStar` runs A* across the road graph using time-based edge costs
8. Result is stored in Redis for 1 hour and returned to the client

### Road Costs

Edge costs are travel time in seconds based on highway type:

| Road Type | Speed |
|---|---|
| Motorway | 100 km/h |
| Trunk / Primary | 80 km/h |
| Secondary | 60 km/h |
| Tertiary / Residential | 30 km/h |
| Living Street | 10 km/h |

## Load Testing

Load tests use k6 and target the `POST /api/route` endpoint with 20 unique NYC routes.

```bash
make load-test
```

Or with custom options:

```bash
k6 run -e BASE_URL=http://localhost:8000 testing/load-test.js
```

### Results (50 VUs, 3.5 minutes)

- **p95 latency**: ~10ms (Redis cache hits)
- **Failure rate**: < 0.3%
- **Throughput**: ~22 requests/second

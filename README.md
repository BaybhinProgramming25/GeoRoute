# SimpleMaps

A self-hosted vehicle routing application built on OpenStreetMap data. Routes are computed using A* pathfinding via pgRouting and PostGIS — no third-party routing APIs.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core (C#) |
| Frontend | React + Leaflet |
| Database | PostgreSQL + PostGIS + pgRouting |
| Cache | Redis |
| Auth | JWT (access + refresh tokens) |
| Data | OpenStreetMap via osm2pgsql |
| Infrastructure | Docker + Docker Compose |
| Load Testing | k6 |

## Features

- A* pathfinding on real OSM road data with per-road-type speed costs
- One-way street support via OSM `oneway` tags
- Turn-by-turn instructions with angle-based turn detection (left, right, keep left, etc.)
- Redis caching with cache locking to prevent thundering herd under concurrent load
- Step markers on the map with hover tooltips showing instructions
- JWT authentication with refresh token rotation

## Prerequisites

- Docker + Docker Compose
- An OSM `.pbf` file for your target region placed at `data/region.osm.pbf`
- `dotnet ef` CLI (for running EF migrations locally)

Download OSM data from [Geofabrik](https://download.geofabrik.de/).

## Setup

For a fresh setup run:

```bash
make bootstrap
```

This will:
1. Build and start all containers
2. Run EF migrations to create the users table
3. Create `routing_db` and enable PostGIS, pgRouting, and hstore extensions
4. Import OSM data into PostgreSQL
5. Build the routing topology (creates `ways` and `ways_vertices_pgr` tables)

The OSM import and topology build can take several minutes depending on the size of your region.

## Environment Variables

Create a `backend/.env` file with the following:

```env
POSTGRES_USER=youruser
POSTGRES_PASSWORD=yourpassword
POSTGRES_DB=yourdbname

CONNECTION_STRING=Host=postgres;Database=POSTGRES_DB;Username=POSTGRES_USER;Password=POSTGRES_PASSWORD
ROUTING_CONNECTION_STRING=Host=postgres;Database=routing_db;Username=POSTGRES_USER;Password=POSTGRES_PASSWORD

JWT__AccessSecret=your-access-secret
JWT__RefreshSecret=your-refresh-secret

REDIS_CONNECTION=redis:6379
```

## Makefile Commands

| Command | Description |
|---|---|
| `make bootstrap` | Full fresh setup — builds, migrates, imports OSM, builds topology |
| `make up` | Start all containers with rebuild |
| `make down` | Stop and remove containers |
| `make restart` | Restart all containers |
| `make logs` | Tail container logs |
| `make migrate` | Run EF database migrations |
| `make setup-routing` | Run OSM import and build routing topology |
| `make rebuild-topology` | Rebuild routing topology without re-importing OSM data |
| `make load-test` | Run k6 load test against the route endpoint |

## Architecture

```
Browser → Nginx (frontend) → React App
                           → /api/* → ASP.NET Core Backend
                                    → Redis (cache + sessions)
                                    → PostgreSQL (users, routing)
```

### Routing Pipeline

1. User enters start and destination addresses
2. Frontend geocodes them via Nominatim (OSM geocoding API)
3. Coordinates are sent to `POST /api/route`
4. Backend checks Redis cache — cache hit returns instantly
5. On cache miss, a Redis lock is acquired to prevent concurrent duplicate queries
6. `FindNearestNode` finds the closest routable road node for each coordinate
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
k6 run -e EMAIL=YOUR_EMAIL -e PASSWORD=YOUR_PASSWORD -e BASE_URL=http://localhost:8000 testing/load-test.js
```

### Results (50 VUs, 3.5 minutes)

- **p95 latency**: ~10ms (Redis cache hits)
- **Failure rate**: < 0.3%
- **Throughput**: ~22 requests/second

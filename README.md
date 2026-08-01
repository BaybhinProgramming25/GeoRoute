# GeoRoute

A self-hosted vehicle routing application built on OpenStreetMap data. Routes are computed using A* pathfinding via pgRouting and PostGIS — no third-party routing APIs.

> **Coverage:** routing works strictly within **New York State** — the deployed instance imports the New York OSM extract, and the API rejects coordinates outside it. To serve a different region, import that region's extract and adjust the bounding box (see below).

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | Spring Boot (Java 21) |
| Frontend | React + Leaflet |
| Database | PostgreSQL + PostGIS + pgRouting |
| Cache | Redis |
| Data | OpenStreetMap via osm2pgsql |
| Reverse Proxy / TLS | Caddy (automatic HTTPS in production) |
| Infrastructure | Docker + Docker Compose |
| Load Testing | k6 |

## Features

- A* pathfinding on real OSM road data with per-road-type speed costs
- One-way street support via OSM `oneway` tags
- Turn-by-turn instructions with angle-based turn detection (left, right, keep left, etc.)
- Redis caching (128 MB LRU cap) with cache locking to prevent thundering herd under concurrent load
- Per-IP rate limiting on the route API (nginx, 5 req/s with burst)
- Request validation (coordinates must be finite and within the served region)
- Automatic HTTPS via Caddy + Let's Encrypt when a domain is configured
- Step markers on the map with hover tooltips showing instructions

## Prerequisites

- Docker + Docker Compose (Compose v2)
- An OSM `.pbf` extract for your target region, placed at `data/region.osm.pbf` — download from [Geofabrik](https://download.geofabrik.de/)

## Running Locally

**1. Create `backend/.env`:**

```env
POSTGRES_USER=georoutemaps
POSTGRES_PASSWORD=anything-for-local
POSTGRES_DB=routing_db
```

**2. Build and start the stack:**

```bash
docker compose up -d --build
```

The app is now at **http://localhost** — the frontend works immediately, but route queries need map data first.

**3. Import the OSM data (one-time per machine):**

```bash
docker compose --profile routing-import up osm-routing-import
```

**4. Build the routing topology:**

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < backend/db/setup_routing.sql
```

The import and topology build can each take a while depending on region size (tens of minutes for a large metro extract). Both results persist in the `postgres-data` volume, so this is a one-time cost — after it completes, routing works and survives restarts/reboots.

To start over from scratch (wipes the database): `docker compose down -v`, then repeat from step 2.

## Environment Variables

**`backend/.env`** (required — used by the backend, postgres, and the importer):

```env
POSTGRES_USER=georoutemaps
POSTGRES_PASSWORD=use-a-strong-random-value-in-production
POSTGRES_DB=routing_db

# Production only:
CORS_ALLOWED_ORIGINS=https://yourdomain.com

# Optional overrides (defaults shown)
ROUTING_DB_URL=jdbc:postgresql://postgres:5432/routing_db
REDIS_HOST=redis
REDIS_PORT=6379
```

**Root `.env`** (optional — production only):

```env
SITE_ADDRESS=yourdomain.com
```

When `SITE_ADDRESS` is unset, Caddy serves plain HTTP on port 80 (local development). When set to a domain whose DNS points at the host, Caddy automatically obtains and renews a Let's Encrypt certificate and redirects HTTP to HTTPS.

Postgres credentials are baked into the data volume on first startup — changing them later requires `docker compose down -v` and a re-import.

## Routing Pipeline

1. User enters start and destination addresses
2. Frontend geocodes them via Nominatim (OSM geocoding API)
3. Coordinates are sent to `POST /api/route`
4. Backend validates the coordinates (finite, within the served region's bounding box)
5. Backend checks Redis cache — cache hit returns instantly
6. On cache miss, a Redis lock is acquired to prevent concurrent duplicate queries; waiters poll briefly, then return `503` with `Retry-After`
7. `findNearestNode` finds the closest routable road node for each coordinate
8. `pgr_aStar` runs A* across the road graph using time-based edge costs
9. Result is stored in Redis for 1 hour and returned to the client

The service bounding box defaults to New York State — adjust the `MIN/MAX_LAT/LON` constants in `RouteController.java` if you import a different region.

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

Load tests use k6 and target the `POST /api/route` endpoint with 20 unique NYC routes:

```bash
k6 run -e BASE_URL=http://localhost testing/load-test.js
```

Note: requests go through the nginx rate limit (5 req/s per IP), so high-VU runs from a single machine will see `429`s by design. To benchmark the backend itself, temporarily raise the limit in `frontend/nginx.conf`.
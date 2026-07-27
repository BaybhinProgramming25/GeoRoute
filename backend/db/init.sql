-- Runs on first startup against the database named by POSTGRES_DB,
-- which the postgres entrypoint has already created.
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS pgrouting;

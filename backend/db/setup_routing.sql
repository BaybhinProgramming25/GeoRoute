-- Extract routable roads from OSM data
CREATE TABLE ways AS
SELECT osm_id, highway, name, ST_Transform(way, 4326) AS geom
FROM planet_osm_line
WHERE highway IN (
    'motorway', 'motorway_link',
    'trunk', 'trunk_link',
    'primary', 'primary_link',
    'secondary', 'secondary_link',
    'tertiary', 'tertiary_link',
    'residential', 'living_street',
    'footway', 'cycleway', 'path',
    'service', 'unclassified'
);

-- Add pgRouting required columns
ALTER TABLE ways ADD COLUMN id SERIAL PRIMARY KEY;
ALTER TABLE ways ADD COLUMN source INTEGER;
ALTER TABLE ways ADD COLUMN target INTEGER;
ALTER TABLE ways ADD COLUMN cost FLOAT;
ALTER TABLE ways ADD COLUMN reverse_cost FLOAT;
ALTER TABLE ways ADD COLUMN x1 FLOAT;
ALTER TABLE ways ADD COLUMN y1 FLOAT;
ALTER TABLE ways ADD COLUMN x2 FLOAT;
ALTER TABLE ways ADD COLUMN y2 FLOAT;

-- Populate coordinate columns for A*
UPDATE ways SET
    x1 = ST_X(ST_StartPoint(geom)),
    y1 = ST_Y(ST_StartPoint(geom)),
    x2 = ST_X(ST_EndPoint(geom)),
    y2 = ST_Y(ST_EndPoint(geom));

-- Populate cost (length in meters using geography cast)
UPDATE ways SET
    cost = ST_Length(geom::geography),
    reverse_cost = ST_Length(geom::geography);

-- Build the topology (creates ways_vertices_pgr table with all nodes)
SELECT pgr_createTopology('ways', 0.00001, 'geom', 'id');

-- Index for performance
CREATE INDEX ON ways (source);
CREATE INDEX ON ways (target);
CREATE INDEX ON ways USING GIST (geom);

-- Extract routable roads from OSM data
CREATE TABLE ways AS
SELECT osm_id, highway, name, tags->'oneway' AS oneway, ST_Transform(way, 4326) AS geom
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

-- Motorways are implicitly one-way
UPDATE ways SET oneway = 'yes' WHERE oneway IS NULL AND highway IN ('motorway', 'motorway_link');

-- Populate coordinate columns for A*
UPDATE ways SET
    x1 = ST_X(ST_StartPoint(geom)),
    y1 = ST_Y(ST_StartPoint(geom)),
    x2 = ST_X(ST_EndPoint(geom)),
    y2 = ST_Y(ST_EndPoint(geom));

-- Populate cost as travel time in seconds based on road type
UPDATE ways SET
    cost = CASE WHEN oneway IN ('-1', 'reverse') THEN 1e10 ELSE ST_Length(geom::geography) END / CASE highway
        WHEN 'motorway'       THEN 27.8 
        WHEN 'motorway_link'  THEN 22.2  
        WHEN 'trunk'          THEN 22.2
        WHEN 'trunk_link'     THEN 16.7  
        WHEN 'primary'        THEN 16.7
        WHEN 'primary_link'   THEN 13.9  
        WHEN 'secondary'      THEN 13.9
        WHEN 'secondary_link' THEN 11.1  
        WHEN 'tertiary'       THEN 11.1
        WHEN 'tertiary_link'  THEN 8.3   
        WHEN 'residential'    THEN 8.3
        WHEN 'living_street'  THEN 2.8   
        ELSE 8.3
    END,
    reverse_cost = CASE
        WHEN oneway IN ('yes', '1', 'true')     THEN 1e10
        WHEN oneway IN ('-1', 'reverse')         THEN ST_Length(geom::geography) / CASE highway
            WHEN 'motorway'       THEN 27.8
            WHEN 'motorway_link'  THEN 22.2
            WHEN 'trunk'          THEN 22.2
            WHEN 'trunk_link'     THEN 16.7
            WHEN 'primary'        THEN 16.7
            WHEN 'primary_link'   THEN 13.9
            WHEN 'secondary'      THEN 13.9
            WHEN 'secondary_link' THEN 11.1
            WHEN 'tertiary'       THEN 11.1
            WHEN 'tertiary_link'  THEN 8.3
            WHEN 'residential'    THEN 8.3
            WHEN 'living_street'  THEN 2.8
            ELSE 8.3
        END
        ELSE ST_Length(geom::geography) / CASE highway
            WHEN 'motorway'       THEN 27.8
            WHEN 'motorway_link'  THEN 22.2
            WHEN 'trunk'          THEN 22.2
            WHEN 'trunk_link'     THEN 16.7
            WHEN 'primary'        THEN 16.7
            WHEN 'primary_link'   THEN 13.9
            WHEN 'secondary'      THEN 13.9
            WHEN 'secondary_link' THEN 11.1
            WHEN 'tertiary'       THEN 11.1
            WHEN 'tertiary_link'  THEN 8.3
            WHEN 'residential'    THEN 8.3
            WHEN 'living_street'  THEN 2.8
            ELSE 8.3
        END
    END;

-- Build the topology (creates ways_vertices_pgr table with all nodes)
SELECT pgr_createTopology('ways', 0.00001, 'geom', 'id');

-- Index for performance
CREATE INDEX ON ways (source);
CREATE INDEX ON ways (target);
CREATE INDEX ON ways USING GIST (geom);
CREATE INDEX ON ways_vertices_pgr USING GIST (the_geom);

-- Mark vertices on the giant connected component so nearest-node snapping
-- never picks a node on a disconnected fragment (parking lots, gated roads),
-- which would make every route from that node fail.
ALTER TABLE ways_vertices_pgr ADD COLUMN main_component BOOLEAN NOT NULL DEFAULT false;

CREATE TEMP TABLE comp AS
SELECT node, component
FROM pgr_connectedComponents(
  'SELECT id, source, target, cost, reverse_cost FROM ways WHERE source != target'
);

WITH giant AS (
  SELECT component FROM comp GROUP BY component ORDER BY count(*) DESC LIMIT 1
)
UPDATE ways_vertices_pgr v SET main_component = true
FROM comp c, giant g
WHERE c.node = v.id AND c.component = g.component;

CREATE INDEX ON ways_vertices_pgr (main_component);

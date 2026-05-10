using System.Text.Json;
using Npgsql;
using EZTravel.Models;

namespace EZTravel.Services;

public class RoutingService(IConfiguration config)
{
    private readonly string _connString =
        Environment.GetEnvironmentVariable("ROUTING_CONNECTION_STRING")
        ?? config.GetConnectionString("Routing")!;

    public async Task<RouteResult> GetRoute(Coordinate source, Coordinate destination)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var sourceNode = await FindNearestNode(conn, source.Lat, source.Lon);
        var destNode = await FindNearestNode(conn, destination.Lat, destination.Lon);

        var (geometry, steps, totalDistance, totalDuration) = await RunAStar(conn, sourceNode, destNode);

        return new RouteResult(geometry, steps, totalDistance, totalDuration);
    }

    private static async Task<long> FindNearestNode(NpgsqlConnection conn, double lat, double lon)
    {
        await using var cmd = new NpgsqlCommand("""
            WITH nearby AS (
                SELECT id, the_geom FROM ways_vertices_pgr
                ORDER BY the_geom <-> ST_SetSRID(ST_Point(@lon, @lat), 4326)
                LIMIT 200
            )
            SELECT n.id FROM nearby n
            WHERE EXISTS (
                SELECT 1 FROM ways w
                WHERE (w.source = n.id OR w.target = n.id)
                AND w.highway NOT IN ('footway', 'cycleway', 'path', 'service')
            )
            ORDER BY n.the_geom <-> ST_SetSRID(ST_Point(@lon, @lat), 4326) LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue("lon", lon);
        cmd.Parameters.AddWithValue("lat", lat);
        var result = await cmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("No routable road found near the given location.");
        return (long)result;
    }

    private static async Task<(List<Coordinate>, List<RouteStep>, double, double)> RunAStar(NpgsqlConnection conn, long sourceNode, long destNode)
    {
        const string sql = """
            SELECT
                CASE WHEN r.node = w.source
                     THEN ST_AsGeoJSON(w.geom)
                     ELSE ST_AsGeoJSON(ST_Reverse(w.geom))
                END AS geojson,
                r.cost,
                w.name,
                ST_Length(w.geom::geography) AS distance_m
            FROM pgr_aStar(
                'SELECT id, source, target, cost, reverse_cost, x1, y1, x2, y2 FROM ways WHERE source != target',
                @source, @dest, directed := true
            ) r
            JOIN ways w ON r.edge = w.id
            WHERE r.edge != -1
            ORDER BY r.seq
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("source", sourceNode);
        cmd.Parameters.AddWithValue("dest", destNode);

        var geometry = new List<Coordinate>();
        var steps = new List<RouteStep>();
        double totalDuration = 0;
        double totalDistance = 0;
        int coordIndex = 0;
        List<Coordinate>? prevCoords = null;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var geoJson = reader.GetString(0);
            var cost = reader.GetDouble(1);
            var name = reader.IsDBNull(2) ? "Unnamed road" : reader.GetString(2);
            var distanceM = reader.GetDouble(3);

            var coords = ParseGeoJsonCoords(geoJson);
            int startIndex = coordIndex;
            geometry.AddRange(coords);
            coordIndex += coords.Count;
            totalDuration += cost;
            totalDistance += distanceM;

            var instruction = GetInstruction(prevCoords, coords, name);
            if (steps.Count > 0 && steps[^1].Instruction == instruction)
                steps[^1] = new RouteStep(instruction, steps[^1].Distance + distanceM, [steps[^1].WayPoints[0], coordIndex - 1]);
            else
                steps.Add(new RouteStep(instruction, distanceM, [startIndex, coordIndex - 1]));

            prevCoords = coords;
        }

        if (geometry.Count == 0)
            throw new InvalidOperationException("No route found between the given locations.");

        return (geometry, steps, totalDistance, totalDuration);
    }

    private static string GetInstruction(List<Coordinate>? prevCoords, List<Coordinate> currCoords, string roadName)
    {
        if (prevCoords == null || prevCoords.Count < 2 || currCoords.Count < 2)
            return $"Continue on {roadName}";

        var p0 = prevCoords[^2];
        var p1 = prevCoords[^1];
        var p2 = currCoords[0];
        var p3 = currCoords[1];

        double dx1 = p1.Lon - p0.Lon;
        double dy1 = p1.Lat - p0.Lat;
        double dx2 = p3.Lon - p2.Lon;
        double dy2 = p3.Lat - p2.Lat;

        double cross = dx1 * dy2 - dy1 * dx2;
        double dot = dx1 * dx2 + dy1 * dy2;
        double angle = Math.Atan2(cross, dot) * 180 / Math.PI;

        return angle switch
        {
            > 150 or < -150 => $"Make a U-turn onto {roadName}",
            > 45  => $"Turn left onto {roadName}",
            > 15  => $"Keep left onto {roadName}",
            < -45 => $"Turn right onto {roadName}",
            < -15 => $"Keep right onto {roadName}",
            _     => $"Continue on {roadName}",
        };
    }

    private static List<Coordinate> ParseGeoJsonCoords(string geoJson)
    {
        using var doc = JsonDocument.Parse(geoJson);
        return doc.RootElement
            .GetProperty("coordinates")
            .EnumerateArray()
            .Select(c => new Coordinate(c[1].GetDouble(), c[0].GetDouble()))
            .ToList();
    }
}

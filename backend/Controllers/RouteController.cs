using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using EZTravel.Models;

namespace EZTravel.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class RouteController(IDatabase redis, HttpClient httpClient, IConfiguration config) : ControllerBase
{
    private static readonly Dictionary<string, string> Profiles = new()
    {
        ["walk"] = "foot-walking",
        ["bike"] = "cycling-regular",
    };

    [HttpPost("route")]
    public async Task<IActionResult> GetRoute([FromBody] RouteRequest req)
    {
        if (!Profiles.TryGetValue(req.Mode, out var profile))
            return BadRequest(new { message = "Invalid mode. Use \"walk\" or \"bike\"" });

        var cacheKey = $"route:{req.Mode}:{req.Source.Lon},{req.Source.Lat}:{req.Destination.Lon},{req.Destination.Lat}";
        var cached = await redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var cachedResult = JsonSerializer.Deserialize<RouteResult>(cached!);
            return Ok(cachedResult! with { FromCache = true });
        }

        var body = JsonSerializer.Serialize(new
        {
            coordinates = new[] { new[] { req.Source.Lon, req.Source.Lat }, new[] { req.Destination.Lon, req.Destination.Lat } },
            radiuses = new[] { -1, -1 },
            preference = "fastest",
            options = new { avoid_features = new[] { "ferries" } },
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.openrouteservice.org/v2/directions/{profile}/geojson")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", config["ORS_API_KEY"]);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return StatusCode(500, new { message = "Failed to get route from ORS" });

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var feature = doc.RootElement.GetProperty("features")[0];
        var coords = feature.GetProperty("geometry").GetProperty("coordinates");
        var summary = feature.GetProperty("properties").GetProperty("summary");

        var geometry = coords.EnumerateArray()
            .Select(c => new Coordinate(c[1].GetDouble(), c[0].GetDouble()))
            .ToList();

        var steps = feature.GetProperty("properties").GetProperty("segments")
            .EnumerateArray()
            .SelectMany(s => s.GetProperty("steps").EnumerateArray())
            .Select(s => new RouteStep(
                s.GetProperty("instruction").GetString()!,
                s.GetProperty("distance").GetDouble(),
                s.GetProperty("way_points").EnumerateArray().Select(w => w.GetInt32()).ToArray()))
            .ToList();

        var result = new RouteResult(geometry, steps, summary.GetProperty("distance").GetDouble(), summary.GetProperty("duration").GetDouble());

        await redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromHours(1));
        return Ok(result);
    }
}

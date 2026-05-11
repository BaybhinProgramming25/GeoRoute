using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using EZTravel.Models;
using EZTravel.Services;

namespace EZTravel.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class RouteController(IDatabase redis, RoutingService routingService) : ControllerBase
{
    [HttpPost("route")]
    public async Task<IActionResult> GetRoute([FromBody] RouteRequest req)
    {
        var cacheKey = $"route:{req.Source.Lat},{req.Source.Lon}:{req.Destination.Lat},{req.Destination.Lon}";
        var lockKey = $"lock:{cacheKey}";


        var cached = await redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return Ok(JsonSerializer.Deserialize<RouteResult>(cached!));


        var acquired = await redis.StringSetAsync(lockKey, "1", TimeSpan.FromSeconds(30), When.NotExists);
        if (!acquired)
        {
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(500);
                cached = await redis.StringGetAsync(cacheKey);
                if (cached.HasValue)
                    return Ok(JsonSerializer.Deserialize<RouteResult>(cached!));
            }
            return StatusCode(503, new { message = "Route computation timed out, please retry." });
        }

        try
        {
            var result = await routingService.GetRoute(req.Source, req.Destination);
            await redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromHours(1));
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return StatusCode(500, new { message = "Route calculation failed" });
        }
        finally
        {
            await redis.KeyDeleteAsync(lockKey);
        }
    }
}

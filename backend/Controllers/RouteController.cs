using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using EZTravel.Models;
using EZTravel.Services;

namespace EZTravel.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class RouteController(IDatabase redis, HttpClient httpClient, IConfiguration config, RoutingService routingService) : ControllerBase
{
    private static readonly Dictionary<string, string> Profiles = new()
    {
        ["walk"] = "foot-walking",
        ["bike"] = "cycling-regular",
    };

    [HttpPost("route")]
    public async Task<IActionResult> GetTestRoute([FromBody] RouteRequest req)
    {
        if (!Profiles.ContainsKey(req.Mode))
            return BadRequest(new { message = "Invalid mode. Use \"walk\" or \"bike\"" });

        try
        {
            var result = await routingService.GetRoute(req.Source, req.Destination, req.Mode);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return StatusCode(500, new { message = "Route calculation failed" });
        }
    }
}

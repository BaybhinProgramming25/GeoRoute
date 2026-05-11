using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EZTravel.Models;
using EZTravel.Services;

namespace EZTravel.Controllers;

[ApiController]
[Route("api")]
public class AuthController(UserService userService, TokenService tokenService) : ControllerBase
{
    [HttpPost("signup")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest req)
    {
        if (await userService.GetByEmailAsync(req.Email) != null)
            return Conflict(new { message = "Email already in use" });

        var hashed = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var user = await userService.CreateAsync(req.Username, req.Email, hashed);

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken(user);
        await userService.SaveRefreshTokenAsync(user.Id, refreshToken);

        SetRefreshCookie(refreshToken);
        return Ok(new AuthResponse(accessToken, new UserDto(user.Id, user.Username, user.Email)));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await userService.GetByEmailAsync(req.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
            return Unauthorized(new { message = "Invalid credentials" });

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken(user);
        await userService.SaveRefreshTokenAsync(user.Id, refreshToken);

        SetRefreshCookie(refreshToken);
        return Ok(new AuthResponse(accessToken, new UserDto(user.Id, user.Username, user.Email)));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var token = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(token)) return Unauthorized(new { message = "No refresh token" });

        var principal = tokenService.ValidateRefreshToken(token);
        if (principal == null) return Unauthorized(new { message = "Invalid refresh token" });

        var user = await userService.GetByRefreshTokenAsync(token);
        if (user == null) return Unauthorized(new { message = "Token not recognised" });

        var newAccess = tokenService.GenerateAccessToken(user);
        var newRefresh = tokenService.GenerateRefreshToken(user);
        await userService.SaveRefreshTokenAsync(user.Id, newRefresh);

        SetRefreshCookie(newRefresh);
        return Ok(new AuthResponse(newAccess, new UserDto(user.Id, user.Username, user.Email)));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(token))
        {
            var user = await userService.GetByRefreshTokenAsync(token);
            if (user != null) await userService.SaveRefreshTokenAsync(user.Id, null);
        }
        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "Logged out" });
    }

    private void SetRefreshCookie(string token) =>
        Response.Cookies.Append("refreshToken", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });
}

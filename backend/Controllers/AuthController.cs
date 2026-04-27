using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;

    public AuthController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost]
    public ActionResult<string> Login(LoginRequest request)
    {
        // Hardcoded testing
        // 1 is a placeholder for the user ID that we get from the database
        if (request.Email == "test@test.com" && request.Password == "password")
        {
            var token = _tokenService.GenerateToken("1", request.Email);
            return Ok(new {token});
        }
        
        return Unauthorized();
    }

    [HttpPost]
    public ActionResult<string> Signup(SignupRequest request)
    {
        // 1 is a placeholder for the user id 
        var token = _tokenService.GenerateToken("1", request.Email);
        return Ok(new { token });
    }
}

public record LoginRequest(string Email, string Password);
public record SignupRequest(string Firstname, string Lastname, string Email, string Password);
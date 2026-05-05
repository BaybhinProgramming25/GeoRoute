namespace EZTravel.Models
{
    public record SignupRequest(string Username, string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record AuthResponse(string AccessToken, UserDto User);
    public record UserDto(int Id, string Username, string Email);
}

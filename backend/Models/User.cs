public class User
{
    public int Id { get; set; }
    public required string Firstname { get; set; } = string.Empty; 

    public required string Lastname { get; set; } = string.Empty;

    public required string Email { get; set; } = string.Empty;

    public required string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}
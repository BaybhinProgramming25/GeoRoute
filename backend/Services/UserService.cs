using Microsoft.EntityFrameworkCore;
using EZTravel.Configs;
using EZTravel.Models;

namespace EZTravel.Services;

public class UserService(AppDbContext db)
{
    public Task<User?> GetByEmailAsync(string email) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public Task<User?> GetByIdAsync(int id) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User> CreateAsync(string username, string email, string hashedPassword)
    {
        var user = new User { Username = username, Email = email, Password = hashedPassword };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task SaveRefreshTokenAsync(int userId, string? token)
    {
        var user = await db.Users.FindAsync(userId);
        if (user == null) return;
        user.RefreshToken = token;
        await db.SaveChangesAsync();
    }

    public Task<User?> GetByRefreshTokenAsync(string token) =>
        db.Users.FirstOrDefaultAsync(u => u.RefreshToken == token);
}

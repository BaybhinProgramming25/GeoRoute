using Microsoft.EntityFrameworkCore;
using EZTravel.Models;

namespace EZTravel.Configs; 
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Username).HasColumnName("username");
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.Password).HasColumnName("password");
        });
    }
}
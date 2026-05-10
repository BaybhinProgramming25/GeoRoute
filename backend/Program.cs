using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using EZTravel.Configs;
using EZTravel.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true);

builder.Services.AddControllers();

builder.Services.AddCors(options => 
    options.AddDefaultPolicy(policy => 
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme) 
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, 
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:AccessSecret"]!)),
            ValidateIssuer = false, 
            ValidateAudience = false, 
            ClockSkew = TimeSpan.Zero

        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect($"{builder.Configuration["REDIS_HOST"]}:{builder.Configuration["REDIS_PORT"]}"));

builder.Services.AddScoped<IDatabase>(sp => 
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        Environment.GetEnvironmentVariable("CONNECTION_STRING")
        ?? builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
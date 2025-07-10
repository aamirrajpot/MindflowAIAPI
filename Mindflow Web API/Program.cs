using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Mindflow_Web_API.EndPoints;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()  // Logs to Console
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day) // Logs to a file
    .CreateLogger();
builder.Host.UseSerilog(); // Replace built-in logging with Serilog


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<MindflowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString); // Changed from UseNpgsql to UseSqlServer
});

builder.Services.AddTransient<IMovieService, MovieService>();

// Add JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});
builder.Services.AddAuthorization();

// Register UserService
builder.Services.AddTransient<IUserService, UserService>();

// Register EmailService
builder.Services.AddTransient<IEmailService, EmailService>();

// Register WellnessCheckInService
builder.Services.AddTransient<IWellnessCheckInService, WellnessCheckInService>();

// Register ExternalAuthService
builder.Services.AddTransient<IExternalAuthService, ExternalAuthService>();

// Register AdminSeedService
builder.Services.AddTransient<IAdminSeedService, AdminSeedService>();

// Add CORS policy to allow all
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable static file serving
app.UseStaticFiles();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Seeding configurations.
    await using (var serviceScope = app.Services.CreateAsyncScope())
    await using (var dbContext = serviceScope.ServiceProvider.GetRequiredService<MindflowDbContext>())
    {
        // Apply any pending migrations
        await dbContext.Database.MigrateAsync();

        // Seed admin user
        var adminSeedService = serviceScope.ServiceProvider.GetRequiredService<IAdminSeedService>();
        await adminSeedService.SeedAdminUserAsync();
    }

    app.MapOpenApi();
    app.MapScalarApiReference();

}

app.UseHttpsRedirection();
// Enable CORS for all
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Map root URL to static page only in production
if (app.Environment.IsProduction())
{
    app.MapGet("/", context => {
        context.Response.Redirect("/index.html");
        return Task.CompletedTask;
    });
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");
app.MapMovieEndpoints();
app.MapUserEndpoints();
app.MapWellnessCheckInEndpoints();

app.Run();

// Print a message to indicate the API is running
Console.WriteLine("Mindflow Web API is running. Access your endpoints at the configured URLs.");

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

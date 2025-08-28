using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;
using Mindflow_Web_API.EndPoints;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Middleware;
using Mindflow_Web_API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Stripe;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()  // Logs to Console
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day) // Logs to a file
    .CreateLogger();
builder.Host.UseSerilog(); // Replace built-in logging with Serilog


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Mindflow Web API", Version = "v1" });
    // Add JWT Bearer support
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddDbContext<MindflowDbContext>(options =>
{
    // Always place the SQLite DB in the project's root (content root)
    var contentRoot = builder.Environment.ContentRootPath;
    var dbPath = Path.Combine(contentRoot, "mindflow.db");
    var connectionString = $"Data Source={dbPath}";
    options.UseSqlite(connectionString);
});

// Remove MovieService registration
// builder.Services.AddTransient<IMovieService, MovieService>();
builder.Services.AddTransient<IOllamaService, OllamaService>();

// Add JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var keyString = jwtSettings["Key"];
if (string.IsNullOrEmpty(keyString))
    throw new InvalidOperationException("JWT Key is missing from configuration.");

var key = Encoding.UTF8.GetBytes(keyString);
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
builder.Services.AddScoped<IUserService, UserService>();

// Register EmailService
builder.Services.AddTransient<IEmailService, EmailService>();

// Register WellnessCheckInService
builder.Services.AddScoped<IWellnessCheckInService, WellnessCheckInService>();

// Register ExternalAuthService
builder.Services.AddTransient<IExternalAuthService, ExternalAuthService>();

// Register AdminSeedService
builder.Services.AddTransient<IAdminSeedService, AdminSeedService>();

// Register TaskItem Service
builder.Services.AddScoped<ITaskItemService, TaskItemService>();

// Register OllamaService
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
});

// Register SubscriptionService (fully qualify to avoid Stripe.SubscriptionService ambiguity)
builder.Services.AddScoped<Mindflow_Web_API.Services.ISubscriptionService, Mindflow_Web_API.Services.SubscriptionService>();

// Register PaymentService
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Stripe configuration and services
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<CustomerService>();
builder.Services.AddSingleton<ChargeService>();
builder.Services.AddSingleton<PaymentIntentService>();
builder.Services.AddSingleton<PaymentMethodService>();
builder.Services.AddSingleton<EphemeralKeyService>();
builder.Services.AddScoped<IStripeService, StripeService>();

// Register SubscriptionSeedService
builder.Services.AddScoped<SubscriptionSeedService>();

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

// Add global exception handler middleware (must be early in pipeline)
app.UseGlobalExceptionHandler();

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

        // Seed subscription data
        var subscriptionSeedService = serviceScope.ServiceProvider.GetRequiredService<SubscriptionSeedService>();
        await subscriptionSeedService.SeedSubscriptionDataAsync();
    }


}

// Enable Swagger in all environments
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
// Enable CORS for all
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();


// Remove weatherforecast endpoint and summaries array

app.MapUserEndpoints();
app.MapWellnessCheckInEndpoints();
app.MapTaskItemEndpoints();
app.MapSubscriptionEndpoints();
app.MapPaymentEndpoints();
app.MapStripeEndpoints();

app.Run();

// Print a message to indicate the API is running
Console.WriteLine("Mindflow Web API is running. Access your endpoints at the configured URLs.");


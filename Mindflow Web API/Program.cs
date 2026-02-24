using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Models;
using Mindflow_Web_API.EndPoints;
using Mindflow_Web_API.Middleware;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Services;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using Stripe;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // Set minimum log level to Information
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // Reduce Microsoft logs to Warning
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, // Keep logs for 30 days
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        shared: true) // Allow multiple processes to write to the same file
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
// Configure JSON to serialize enums as strings (e.g. "Active" instead of 0)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure SQLite as the database
builder.Services.AddDbContext<MindflowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
// Note: Admin checks are enforced inline in endpoints (matching SubscriptionEndpoints pattern)

// Register UserService
builder.Services.AddScoped<IUserService, UserService>();

// Register EmailService
builder.Services.AddTransient<IEmailService, EmailService>();

// Register TimeSlotHelper and WellnessDataProcessor for improved scheduling
builder.Services.AddScoped<TimeSlotHelper>();
builder.Services.AddScoped<WellnessDataProcessor>();

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

// Register RunPodService
builder.Services.AddHttpClient<IRunPodService, RunPodService>();

// TinyLlama / RunPod services
builder.Services.AddHttpClient<IRunPodService, RunPodService>();
builder.Services.AddHttpClient<ITinyLlamaService, TinyLlamaService>();

// Register OpenAI Service
builder.Services.AddHttpClient<IOpenAIService, OpenAIService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
    if (!baseUrl.EndsWith("/"))
    {
        baseUrl += "/";
    }
    client.BaseAddress = new Uri(baseUrl);
});

// Register HttpClient for Google OAuth
builder.Services.AddHttpClient("google-oauth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ISimpleEncryptionService, SimpleEncryptionService>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
// Caching for LLM responses
builder.Services.AddMemoryCache();

// Brain Dump service
builder.Services.AddScoped<IBrainDumpService, BrainDumpService>();

// Journal service
builder.Services.AddScoped<IJournalService, JournalService>();

// Wellness Snapshot service
builder.Services.AddScoped<IWellnessSnapshotService, WellnessSnapshotService>();

// User data cleanup service (runs weekly)
builder.Services.AddHostedService<UserDataCleanupService>();
// Task reminder background service (sends FCM reminders ~10 minutes before task time)
builder.Services.AddHostedService<TaskReminderService>();
// Brain-dump weekly reminder service
builder.Services.AddHostedService<BrainDumpReminderService>();

// Register SubscriptionService (fully qualify to avoid Stripe.SubscriptionService ambiguity)
builder.Services.AddScoped<Mindflow_Web_API.Services.ISubscriptionService, Mindflow_Web_API.Services.SubscriptionService>();

// Apple App Store Server Notifications (V2) - SignedDataVerifier using Mimo.AppStoreServerLibrary
builder.Services.AddSingleton<SignedDataVerifier>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var envValue = config["Apple:Environment"];
    var environment = string.Equals(envValue, "Sandbox", StringComparison.OrdinalIgnoreCase)
        ? AppStoreEnvironment.Sandbox
        : AppStoreEnvironment.Production;

    var bundleId = config["Apple:BundleId"];
    if (string.IsNullOrWhiteSpace(bundleId))
        throw new InvalidOperationException("Apple:BundleId is not configured.");

    // 2) Derive base directory from DefaultConnection (same as firebase-key.json)
    var connectionString = config.GetConnectionString("DefaultConnection");
    string? dbPath = null;

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dataSourcePart = parts.FirstOrDefault(p =>
            p.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            p.TrimStart().StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase));

        if (dataSourcePart != null)
        {
            var kv = dataSourcePart.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (kv.Length == 2)
                dbPath = kv[1].Trim();
        }
    }

    var baseDir = !string.IsNullOrEmpty(dbPath)
        ? Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory
        : AppContext.BaseDirectory;

    // 3) Look for cert in data folder first, then in secrets
    var certPathNextToDb = Path.Combine(baseDir, "AppleRootCA-G3.cer");
    var certPathInSecrets = Path.Combine(AppContext.BaseDirectory, "Secrets", "AppleRootCA-G3.cer");

    string certPath;
    if (System.IO.File.Exists(certPathNextToDb))
    {
        certPath = certPathNextToDb;
    }
    else if (System.IO.File.Exists(certPathInSecrets))
    {
        certPath = certPathInSecrets;
    }
    else
    {
        throw new InvalidOperationException(
            $"Apple root certificate not found. Tried: {certPathNextToDb}, {certPathInSecrets}");
    }

    var rootCertificatesBytes = System.IO.File.ReadAllBytes(certPath);


    return new SignedDataVerifier(
        rootCertificatesBytes,
        true,
        environment,
        bundleId);
});

// Apple App Store Server API client (for legacy receipt → Get Transaction Info via Mimo only)
// SigningKey: PEM contents, path to .p8, filename in db dir (same as cert), or raw base64
string? GetAppleBaseDirectory(IConfiguration config)
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    string? dbPath = null;
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dataSourcePart = parts.FirstOrDefault(p =>
            p.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            p.TrimStart().StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase));
        if (dataSourcePart != null)
        {
            var kv = dataSourcePart.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (kv.Length == 2) dbPath = kv[1].Trim();
        }
    }
    return !string.IsNullOrEmpty(dbPath) ? Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory : AppContext.BaseDirectory;
}
string? ResolveAppleSigningKeyPem(string? value, string? baseDir)
{
    Log.Information("Apple:SigningKey '{Value}'", value);
    if (string.IsNullOrWhiteSpace(value)) return value;
    if (value.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase))
        return value;
    var path = value.Trim();
    // Try absolute/current path first, then db directory (same as cert), then Secrets
    var toTry = new List<string> { path };
    if (!string.IsNullOrWhiteSpace(baseDir))
    {
        Log.Information("Apple:SigningKey base directory derived from connection string is '{BaseDir}'", baseDir);
        toTry.Add(Path.Combine(baseDir, value));
    }
    Log.Information("Apple:SigningKey raw value='{Raw}', baseDir='{BaseDir}', candidate paths: {Paths}",
        value, baseDir, string.Join(", ", toTry));
    foreach (var p in toTry)
    {
        if (System.IO.File.Exists(p))
        {
            Log.Information("Apple:SigningKey using key file at '{Path}'", p);
            return System.IO.File.ReadAllText(p);
        }
    }
    // Value may be raw base64 (e.g. from appsettings). PEM requires headers for ImportFromPem().
    var base64 = path.Replace("\r", "").Replace("\n", "").Trim();
    if (IsLikelyBase64Key(base64))
    {
        Log.Information("Apple:SigningKey treated as raw base64; wrapping into PEM.");
        return "-----BEGIN PRIVATE KEY-----\n" + base64 + "\n-----END PRIVATE KEY-----";
    }
    Log.Error("Apple:SigningKey could not be resolved to a file or base64 key. Tried paths: {Paths}", string.Join("; ", toTry));
    throw new InvalidOperationException(
        "Apple:SigningKey must be (1) full PEM, (2) path/filename to a .p8 file (looked for in db directory and Secrets), or (3) raw base64. Tried: " + string.Join("; ", toTry));
}

bool IsLikelyBase64Key(string s)
{
    if (string.IsNullOrWhiteSpace(s)) return false;
    // Base64 should only contain A–Z, a–z, 0–9, +, /, =
    foreach (var c in s)
    {
        if (!(char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '='))
            return false;
    }
    // Length should be divisible by 4 for standard base64
    return s.Length % 4 == 0;
}
var appleBaseDir = GetAppleBaseDirectory(builder.Configuration);
var appleSigningKeyRaw = builder.Configuration["Apple:SigningKey"];
Log.Information("Apple:SigningKey AppleSigningKeyRaw '{AppleSigningKeyRaw}'", appleSigningKeyRaw);
var appleSigningKey = ResolveAppleSigningKeyPem(appleSigningKeyRaw, appleBaseDir);
var appleKeyId = builder.Configuration["Apple:KeyId"];
var appleIssuerId = builder.Configuration["Apple:IssuerId"];
var appleBundleId = builder.Configuration["Apple:BundleId"];
var appleEnvValue = builder.Configuration["Apple:Environment"];
var appleDefaultEnv = string.Equals(appleEnvValue, "Sandbox", StringComparison.OrdinalIgnoreCase)
    ? AppStoreEnvironment.Sandbox
    : AppStoreEnvironment.Production;
if (!string.IsNullOrWhiteSpace(appleSigningKey) && !string.IsNullOrWhiteSpace(appleKeyId) && !string.IsNullOrWhiteSpace(appleIssuerId) && !string.IsNullOrWhiteSpace(appleBundleId))
{
    var appleProduction = new AppStoreServerApiClient(appleSigningKey, appleKeyId, appleIssuerId, appleBundleId, AppStoreEnvironment.Production);
    var appleSandbox = new AppStoreServerApiClient(appleSigningKey, appleKeyId, appleIssuerId, appleBundleId, AppStoreEnvironment.Sandbox);
    builder.Services.AddSingleton(new AppleAppStoreApiWrapper(appleProduction, appleSandbox, appleDefaultEnv));
    Log.Information("AppleAppStoreApiWrapper registered with production and sandbox clients. Default environment: {DefaultEnv}", appleDefaultEnv);
}
else
{
    builder.Services.AddSingleton<AppleAppStoreApiWrapper>(new AppleAppStoreApiWrapper(null, null, appleDefaultEnv));
}

// ReceiptUtility from Mimo (stateless, used to extract transaction ID from legacy receipt)
builder.Services.AddSingleton<ReceiptUtility>();

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



// Firebase is initialized inside FcmNotificationService on first use (FIREBASE_ADMIN_JSON or secrets/firebase-key.json).
builder.Services.AddScoped<IFcmNotificationService, FcmNotificationService>();

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

// Ensure database schema exists (apply migrations on startup)
try
{
    await using var scope = app.Services.CreateAsyncScope();
    await using var dbContext = scope.ServiceProvider.GetRequiredService<MindflowDbContext>();
    await dbContext.Database.MigrateAsync();
    Log.Information("EF migrations applied successfully on startup.");
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to apply EF migrations on startup.");
}

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    // Seeding configurations.
    await using (var serviceScope = app.Services.CreateAsyncScope())
    await using (var dbContext = serviceScope.ServiceProvider.GetRequiredService<MindflowDbContext>())
    {
        

        // Seed admin user
        var adminSeedService = serviceScope.ServiceProvider.GetRequiredService<IAdminSeedService>();
        await adminSeedService.SeedDefaultUsersAsync();

        // Seed subscription data
        var subscriptionSeedService = serviceScope.ServiceProvider.GetRequiredService<SubscriptionSeedService>();
        await subscriptionSeedService.SeedSubscriptionDataAsync();
    }


//}


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
app.MapBraindumpDashboardEndpoints();
app.MapSubscriptionEndpoints();
//app.MapPaymentEndpoints();
//app.MapStripeEndpoints();
app.MapRunPodEndpoints();
app.MapGoogleCalendarEndpoints();
app.MapBrainDumpEndpoints();
app.MapJournalEndpoints();
app.MapOpenAIEndpoints();
app.MapFcmNotificationEndpoints();

app.Run();

// Print a message to indicate the API is running
Console.WriteLine("Mindflow Web API is running. Access your endpoints at the configured URLs.");


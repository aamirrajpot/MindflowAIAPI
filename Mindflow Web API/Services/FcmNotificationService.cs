using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Service responsible for sending FCM push notifications and managing
    /// device-token-based delivery for users. Initializes Firebase on first use from
    /// FIREBASE_ADMIN_JSON (env/config) or secrets/firebase-key.json.
    /// </summary>
    public class FcmNotificationService : IFcmNotificationService
    {
        private static readonly object InitLock = new();
        private static bool _initialized;

        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<FcmNotificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public FcmNotificationService(
            MindflowDbContext dbContext,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<FcmNotificationService> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
            EnsureFirebaseInitialized();
        }

        /// <summary>
        /// Ensures Firebase is initialized. Reads credentials from secrets/firebase-key.json.
        /// Called on every request that needs FCM; the Firebase app itself is created only once.
        /// </summary>
        private void EnsureFirebaseInitialized()
        {
            lock (InitLock)
            {
                // If a default app already exists, nothing to do (this will be true after first request).
                try
                {
                    var existingApp = FirebaseApp.DefaultInstance;
                    if (existingApp != null)
                        return;
                }
                catch (InvalidOperationException)
                {
                    // No default app yet, proceed to create it.
                }

                // Derive credential path from the DB path in DefaultConnection
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
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

                if (!string.IsNullOrEmpty(dbPath))
                {
                    _logger.LogInformation("Firebase initialization: parsed DB path from connection string: {DbPath}", dbPath);
                }
                else
                {
                    _logger.LogWarning("Firebase initialization: could not parse DB path from DefaultConnection. Falling back to ContentRootPath.");
                }

                // If we parsed a DB path, use its directory; otherwise fall back to ContentRootPath
                var baseDir = !string.IsNullOrEmpty(dbPath)
                    ? Path.GetDirectoryName(dbPath) ?? _environment.ContentRootPath
                    : _environment.ContentRootPath;

                // Firebase key will live alongside the DB file, e.g. C:\\home\\data\\firebase-key.json
                var secretsPath = Path.Combine(baseDir, "firebase-key.json");

                if (!System.IO.File.Exists(secretsPath))
                {
                    _logger.LogError("Firebase credential file not found at {Path}. Ensure firebase-key.json exists next to the DB.", secretsPath);
                    throw new InvalidOperationException($"Firebase credential file not found at {secretsPath}.");
                }

                _logger.LogInformation("Firebase initialization: found firebase-key.json at {Path}", secretsPath);

                try
                {
                    var baseCredential = GoogleCredential.FromFile(secretsPath);

                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = baseCredential
                    });

                    _initialized = true;
                    _logger.LogInformation("Firebase Admin initialized from secrets/firebase-key.json.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Firebase Admin from secrets/firebase-key.json.");
                    throw;
                }
            }
        }

        public Task<bool> IsFirebaseAvailableAsync()
        {
            try
            {
                var _ = FirebaseApp.DefaultInstance;
                return Task.FromResult(true);
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Firebase initialization state");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Gets the Firebase Messaging instance. Ensures Firebase is initialized first, then returns messaging.
        /// </summary>
        private FirebaseMessaging GetMessaging()
        {
            EnsureFirebaseInitialized();
            try
            {
                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                    throw new InvalidOperationException("Firebase is not initialized. Set FIREBASE_ADMIN_JSON or add secrets/firebase-key.json.");
                return FirebaseMessaging.GetMessaging(app);
            }
            catch (InvalidOperationException ex) when (string.IsNullOrEmpty(ex.Message) || ex.Message.Contains("default", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("DefaultInstance", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Firebase is not initialized. Set FIREBASE_ADMIN_JSON or add secrets/firebase-key.json.", ex);
            }
        }

        public async Task<string> SendToDeviceAsync(
            string deviceToken,
            string title,
            string body,
            IDictionary<string, string>? data = null)
        {
            var message = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data is null ? new Dictionary<string, string>() : new Dictionary<string, string>(data)
            };

            try
            {
                var messaging = GetMessaging();
                var response = await messaging.SendAsync(message);
                _logger.LogInformation("FCM notification sent successfully. Response: {Response}", response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send FCM notification to device token {Token}", deviceToken);
                throw;
            }
        }

        public async Task<int> SendToUserAsync(
            Guid userId,
            string title,
            string body,
            IDictionary<string, string>? data = null)
        {
            var tokens = await _dbContext.FcmDeviceTokens
                .Where(t => t.UserId == userId && t.IsActive)
                .Select(t => t.DeviceToken)
                .Distinct()
                .ToListAsync();

            if (tokens.Count == 0)
            {
                _logger.LogWarning("No active FCM device tokens found for user {UserId}", userId);
                return 0;
            }

            var message = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data is null ? new Dictionary<string, string>() : new Dictionary<string, string>(data)
            };

            try
            {
                var messaging = GetMessaging();
                var response = await messaging.SendEachForMulticastAsync(message);
                var responses = response.Responses ?? Array.Empty<SendResponse>();
                var successCount = responses.Count(r => r.IsSuccess);
                var failureCount = responses.Count - successCount;

                _logger.LogInformation(
                    "FCM multicast sent for user {UserId}. Success: {SuccessCount}, Failure: {FailureCount}",
                    userId,
                    successCount,
                    failureCount);

                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send FCM multicast notification for user {UserId}", userId);
                throw;
            }
        }
    }
}



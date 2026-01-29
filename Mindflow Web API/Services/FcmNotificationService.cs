using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Service responsible for sending FCM push notifications and managing
    /// device-token-based delivery for users.
    /// </summary>
    public class FcmNotificationService : IFcmNotificationService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<FcmNotificationService> _logger;

        private static bool _firebaseInitialized;
        private static readonly object InitLock = new();

        public FcmNotificationService(
            MindflowDbContext dbContext,
            IConfiguration configuration,
            ILogger<FcmNotificationService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;

            EnsureFirebaseInitialized(configuration);
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

        private static void EnsureFirebaseInitialized(IConfiguration configuration)
        {
            if (_firebaseInitialized)
            {
                return;
            }

            // Quick check: if a FirebaseApp default instance already exists, consider initialized
            try
            {
                var _ = FirebaseApp.DefaultInstance;
                _firebaseInitialized = true;
                return;
            }
            catch (InvalidOperationException)
            {
                // not initialized yet
            }

            lock (InitLock)
            {
                if (_firebaseInitialized)
                {
                    return;
                }

                // Another safety check inside the lock
                try
                {
                    var _ = FirebaseApp.DefaultInstance;
                    _firebaseInitialized = true;
                    return;
                }
                catch (InvalidOperationException)
                {
                    // still not initialized
                }

                // Prefer environment variable (FIREBASE_ADMIN_JSON) so Azure App Settings works.
                var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_JSON");
                AppOptions appOptions;

                if (!string.IsNullOrWhiteSpace(firebaseJson))
                {
                    if (!firebaseJson.TrimStart().StartsWith("{"))
                        firebaseJson = Encoding.UTF8.GetString(Convert.FromBase64String(firebaseJson));

                    appOptions = new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(firebaseJson)
                    };
                }
                else
                {
                    var section = configuration.GetSection("Firebase");
                    var credentialsPath = section["CredentialsFilePath"];
                    var projectId = section["ProjectId"];

                    if (string.IsNullOrWhiteSpace(credentialsPath))
                    {
                        throw new InvalidOperationException("Firebase credentials are not configured. Set FIREBASE_ADMIN_JSON app setting or Firebase:CredentialsFilePath.");
                    }

                    appOptions = new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(credentialsPath)
                    };

                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        appOptions.ProjectId = projectId;
                    }
                }

                FirebaseApp.Create(appOptions);
                _firebaseInitialized = true;
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
                var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
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
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                var successCount = response.Responses.Count(r => r.IsSuccess);
                var failureCount = response.Responses.Count - successCount;

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



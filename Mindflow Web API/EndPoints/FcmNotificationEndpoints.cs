using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Services;
using System.Text;
using System.Text.Json;
namespace Mindflow_Web_API.EndPoints
{
    public static class FcmNotificationEndpoints
    {
        public static void MapFcmNotificationEndpoints(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("/api/notifications").WithTags("Notifications");

            api.MapPost("/register-device", RegisterDeviceAsync)
                .RequireAuthorization()
                .WithOpenApi(op =>
                {
                    op.Summary = "Register FCM device token";
                    op.Description = "Registers or updates a device token for the authenticated user.";
                    return op;
                });

            api.MapPost("/send-test", SendTestNotificationAsync)
                .RequireAuthorization()
                .WithOpenApi(op =>
                {
                    op.Summary = "Send test FCM notification";
                    op.Description = "Sends a test notification either to a specific device token or to all devices for a user.";
                    return op;
                });

            api.MapGet("/device-tokens", GetDeviceTokensAsync)
                .RequireAuthorization()
                .WithOpenApi(op =>
                {
                    op.Summary = "Get current user's FCM device tokens";
                    op.Description = "Returns all registered FCM device tokens for the authenticated user.";
                    return op;
                });

            api.MapDelete("/device-tokens", DeleteDeviceTokensAsync)
                .RequireAuthorization()
                .WithOpenApi(op =>
                {
                    op.Summary = "Delete FCM device token(s) for current user";
                    op.Description = "Deletes FCM device tokens for the authenticated user. If deviceToken query parameter is provided, deletes only that token. If omitted, deletes all tokens for the user.";
                    return op;
                });

            api.MapDelete("/device-tokens/purge-all", PurgeAllDeviceTokensAsync)
                .RequireAuthorization()
                .WithOpenApi(op =>
                {
                    op.Summary = "Admin: Purge all FCM device tokens";
                    op.Description = "Deletes all FCM device tokens for all users in the system. Requires Admin role.";
                    return op;
                });

            api.MapGet("/firebase-status", CheckFirebaseStatusAsync)
                .WithOpenApi(op =>
                {
                    op.Summary = "Check Firebase initialization status";
                    op.Description = "Returns whether the Firebase Admin credential has been loaded and FirebaseAdmin is initialized.";
                    return op;
                });
            
            api.MapGet("/firebase-env", GetFirebaseEnvInfoAsync)
                .WithOpenApi(op =>
                {
                    op.Summary = "Read FIREBASE_ADMIN_JSON env var (sanitized)";
                    op.Description = "Decodes FIREBASE_ADMIN_JSON (base64 or raw) and returns non-sensitive fields (project_id, client_email) and a flag for private_key presence. Does NOT return the private_key.";
                    return op;
                });
        }

        private static async Task<IResult> RegisterDeviceAsync(
            RegisterFcmDeviceDto dto,
            MindflowDbContext dbContext,
            HttpContext context,
            ILogger<Program> logger)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                throw ApiExceptions.Unauthorized("User is not authenticated");
            }

            var userIdClaim = context.User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw ApiExceptions.Unauthorized("Invalid user token");
            }

            if (string.IsNullOrWhiteSpace(dto.DeviceToken))
            {
                throw ApiExceptions.ValidationError("DeviceToken is required.");
            }

            var platform = string.IsNullOrWhiteSpace(dto.Platform)
                ? "unknown"
                : dto.Platform.ToLowerInvariant();

            var existing = await dbContext.FcmDeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.DeviceToken == dto.DeviceToken);

            if (existing is null)
            {
                existing = new FcmDeviceToken
                {
                    UserId = userId,
                    DeviceToken = dto.DeviceToken,
                    Platform = platform,
                    IsActive = true
                };
                await dbContext.FcmDeviceTokens.AddAsync(existing);
            }
            else
            {
                existing.Platform = platform;
                existing.IsActive = true;
                existing.UpdateLastModified();
                dbContext.FcmDeviceTokens.Update(existing);
            }

            await dbContext.SaveChangesAsync();

            logger.LogInformation("FCM device token registered for user {UserId} on platform {Platform}", userId, platform);

            return Results.Ok(new
            {
                message = "Device registered successfully.",
                userId,
                platform
            });
        }

        private static async Task<IResult> SendTestNotificationAsync(
            SendTestNotificationDto dto,
            IFcmNotificationService notificationService,
            HttpContext context)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                throw ApiExceptions.Unauthorized("User is not authenticated");
            }

            if (!string.IsNullOrWhiteSpace(dto.DeviceToken))
            {
                var messageId = await notificationService.SendToDeviceAsync(
                    dto.DeviceToken,
                    dto.Title,
                    dto.Body,
                    dto.Data);

                return Results.Ok(new
                {
                    message = "Notification sent to device token.",
                    messageId
                });
            }

            Guid targetUserId;
            if (dto.UserId.HasValue && dto.UserId.Value != Guid.Empty)
            {
                targetUserId = dto.UserId.Value;
            }
            else
            {
                var userIdClaim = context.User.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out targetUserId))
                {
                    throw ApiExceptions.Unauthorized("Invalid user token");
                }
            }

            var successCount = await notificationService.SendToUserAsync(
                targetUserId,
                dto.Title,
                dto.Body,
                dto.Data);

            return Results.Ok(new
            {
                message = "Notification sent to user devices.",
                userId = targetUserId,
                successCount
            });
        }

        private static async Task<IResult> GetDeviceTokensAsync(
            MindflowDbContext dbContext,
            HttpContext context)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                throw ApiExceptions.Unauthorized("User is not authenticated");
            }

            var userIdClaim = context.User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw ApiExceptions.Unauthorized("Invalid user token");
            }

            var tokens = await dbContext.FcmDeviceTokens
                .FromSqlRaw(@"
                    SELECT *
                    FROM FcmDeviceTokens
                    WHERE UserId = {0}", userId)
                .ToListAsync();


            return Results.Ok(tokens);
        }

        private static async Task<IResult> DeleteDeviceTokensAsync(
            [FromQuery] string? deviceToken,
            [FromServices] MindflowDbContext dbContext,
            HttpContext context)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                throw ApiExceptions.Unauthorized("User is not authenticated");
            }

            var userIdClaim = context.User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw ApiExceptions.Unauthorized("Invalid user token");
            }

            // If deviceToken is provided, delete only that token; otherwise delete all tokens for the user
            IQueryable<FcmDeviceToken> query = dbContext.FcmDeviceTokens.Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(deviceToken))
            {
                query = query.Where(t => t.DeviceToken == deviceToken);
            }

            var toRemove = await query.ToListAsync();
            var count = toRemove.Count;

            if (count > 0)
            {
                dbContext.FcmDeviceTokens.RemoveRange(toRemove);
                await dbContext.SaveChangesAsync();
            }

            var message = string.IsNullOrWhiteSpace(deviceToken)
                ? (count > 0 ? $"Deleted all {count} device token(s) for the user." : "No device tokens found for the user.")
                : (count > 0 ? "Deleted the specified device token." : "The specified device token was not found.");

            return Results.Ok(new
            {
                message,
                deletedCount = count
            });
        }

        private static async Task<IResult> PurgeAllDeviceTokensAsync(
            [FromServices] MindflowDbContext dbContext,
            HttpContext context)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                throw ApiExceptions.Unauthorized("User is not authenticated");
            }

            // Only allow Admins to purge all tokens
            var isAdmin = context.User.Claims.Any(c =>
                c.Type == ClaimTypes.Role && c.Value == "Admin");
            if (!isAdmin)
            {
                throw ApiExceptions.Forbidden("Only administrators can purge all FCM device tokens.");
            }

            // Get all tokens and count before deletion
            var allTokens = await dbContext.FcmDeviceTokens.ToListAsync();
            var totalCount = allTokens.Count;

            if (totalCount > 0)
            {
                // Delete all FCM device tokens
                dbContext.FcmDeviceTokens.RemoveRange(allTokens);
                await dbContext.SaveChangesAsync();
            }

            return Results.Ok(new
            {
                message = totalCount > 0
                    ? $"Purged all {totalCount} FCM device token(s) for all users."
                    : "No FCM device tokens found in the system.",
                deletedCount = totalCount
            });
        }

        private static async Task<IResult> CheckFirebaseStatusAsync(
            IFcmNotificationService notificationService)
        {
            var envVar = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_JSON");
            var envVarPresent = !string.IsNullOrWhiteSpace(envVar);
            var initialized = await notificationService.IsFirebaseAvailableAsync();

            return Results.Ok(new
            {
                firebaseEnvVarPresent = envVarPresent,
                firebaseInitialized = initialized
            });
        }

        private static Task<IResult> GetFirebaseEnvInfoAsync()
        {
            var env = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_JSON");
            if (string.IsNullOrWhiteSpace(env))
            {
                return Task.FromResult(Results.Ok(new { present = false }));
            }

            string json = env;
            if (!json.TrimStart().StartsWith("{"))
            {
                try
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(json));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(Results.BadRequest(new { error = "Invalid base64 in FIREBASE_ADMIN_JSON", details = ex.Message }));
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var clientEmail = root.TryGetProperty("client_email", out var ce) ? ce.GetString() : null;
                var projectId = root.TryGetProperty("project_id", out var pi) ? pi.GetString() : null;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                var privateKeyPresent = root.TryGetProperty("private_key", out var pk) && !string.IsNullOrEmpty(pk.GetString());

                return Task.FromResult(Results.Ok(new
                {
                    present = true,
                    type,
                    projectId,
                    clientEmail,
                    privateKeyPresent
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Results.BadRequest(new { error = "Invalid JSON in FIREBASE_ADMIN_JSON", details = ex.Message }));
            }
        }
    }
}



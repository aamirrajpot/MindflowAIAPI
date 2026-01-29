using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Services;

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
    }
}



using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Mindflow_Web_API.EndPoints
{
    public static class SubscriptionEndpoints
    {
        public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
        {
            var subscriptionApi = app.MapGroup("/api/subscriptions").WithTags("Subscriptions");

            // User Subscription Management
            subscriptionApi.MapGet("/overview", async (ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var overview = await subscriptionService.GetSubscriptionOverviewAsync(userId);
                return Results.Ok(overview);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get subscription overview";
                op.Description = "Gets the current user's subscription status and available plans.";
                return op;
            });

            subscriptionApi.MapPost("/subscribe", async (CreateUserSubscriptionDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var subscription = await subscriptionService.CreateUserSubscriptionAsync(userId, dto);
                return Results.Ok(subscription);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Subscribe to a plan";
                op.Description = "Subscribes the authenticated user to a subscription plan.";
                return op;
            });

            subscriptionApi.MapGet("/current", async (ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var subscription = await subscriptionService.GetUserSubscriptionAsync(userId);
                return Results.Ok(subscription);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get current subscription";
                op.Description = "Gets the current user's active subscription.";
                return op;
            });

            // Issue Apple appAccountToken for StoreKit purchases (used to link webhooks to user)
            subscriptionApi.MapPost("/apple/app-account-token", async (ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var token = await subscriptionService.CreateAppleAppAccountTokenAsync(userId);
                return Results.Ok(new { appAccountToken = token });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Issue Apple appAccountToken";
                op.Description = "Issues a GUID appAccountToken for the authenticated user to use in Apple IAPs.";
                return op;
            });

            subscriptionApi.MapPatch("/cancel", async (CancelSubscriptionDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var cancelled = await subscriptionService.CancelUserSubscriptionAsync(userId, dto);
                if (!cancelled)
                    throw ApiExceptions.NotFound("No active subscription found");
                return Results.Ok(new { message = "Subscription cancelled successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Cancel subscription";
                op.Description = "Cancels the current user's active subscription.";
                return op;
            });

            // Admin endpoints for managing plans and features
            var adminSubscriptionApi = app.MapGroup("/api/admin/subscriptions").WithTags("Admin Subscriptions");

            bool IsAdmin(HttpContext ctx)
            {
                var roleClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
                return roleClaim != null && string.Equals(roleClaim.Value, "Admin", StringComparison.OrdinalIgnoreCase);
            }

            // Subscription Plans (Admin)
            adminSubscriptionApi.MapPost("/plans", async (CreateSubscriptionPlanDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var plan = await subscriptionService.CreatePlanAsync(dto);
                return Results.Ok(plan);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Create subscription plan";
                op.Description = "Creates a new subscription plan (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapGet("/plans", async (ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var plans = await subscriptionService.GetAllPlansAsync();
                return Results.Ok(plans);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get all subscription plans";
                op.Description = "Gets all subscription plans (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapGet("/plans/{planId:guid}", async (Guid planId, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var plan = await subscriptionService.GetPlanByIdAsync(planId);
                if (plan == null)
                    throw ApiExceptions.NotFound("Plan not found");
                return Results.Ok(plan);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get subscription plan by ID";
                op.Description = "Gets a specific subscription plan by ID (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapPut("/plans/{planId:guid}", async (Guid planId, UpdateSubscriptionPlanDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var plan = await subscriptionService.UpdatePlanAsync(planId, dto);
                if (plan == null)
                    throw ApiExceptions.NotFound("Plan not found");
                return Results.Ok(plan);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Update subscription plan";
                op.Description = "Updates a subscription plan (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapDelete("/plans/{planId:guid}", async (Guid planId, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var deleted = await subscriptionService.DeletePlanAsync(planId);
                if (!deleted)
                    throw ApiExceptions.NotFound("Plan not found");
                return Results.Ok(new { message = "Plan deleted successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Delete subscription plan";
                op.Description = "Deletes a subscription plan (Admin only).";
                return op;
            });

            // Subscription Features (Admin)
            adminSubscriptionApi.MapPost("/features", async (CreateSubscriptionFeatureDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var feature = await subscriptionService.CreateFeatureAsync(dto);
                return Results.Ok(feature);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Create subscription feature";
                op.Description = "Creates a new subscription feature (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapGet("/features", async (ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var features = await subscriptionService.GetAllFeaturesAsync();
                return Results.Ok(features);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get all subscription features";
                op.Description = "Gets all subscription features (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapGet("/features/{featureId:guid}", async (Guid featureId, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var feature = await subscriptionService.GetFeatureByIdAsync(featureId);
                if (feature == null)
                    throw ApiExceptions.NotFound("Feature not found");
                return Results.Ok(feature);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get subscription feature by ID";
                op.Description = "Gets a specific subscription feature by ID (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapPut("/features/{featureId:guid}", async (Guid featureId, UpdateSubscriptionFeatureDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var feature = await subscriptionService.UpdateFeatureAsync(featureId, dto);
                if (feature == null)
                    throw ApiExceptions.NotFound("Feature not found");
                return Results.Ok(feature);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Update subscription feature";
                op.Description = "Updates a subscription feature (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapDelete("/features/{featureId:guid}", async (Guid featureId, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var deleted = await subscriptionService.DeleteFeatureAsync(featureId);
                if (!deleted)
                    throw ApiExceptions.NotFound("Feature not found");
                return Results.Ok(new { message = "Feature deleted successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Delete subscription feature";
                op.Description = "Deletes a subscription feature (Admin only).";
                return op;
            });

            // Plan Features (Admin)
            adminSubscriptionApi.MapPost("/plans/{planId:guid}/features", async (Guid planId, CreatePlanFeatureDto dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                if (dto.PlanId != planId)
                    throw ApiExceptions.ValidationError("Plan ID mismatch");
                var planFeature = await subscriptionService.AddFeatureToPlanAsync(dto);
                return Results.Ok(planFeature);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Add feature to plan";
                op.Description = "Adds a feature to a subscription plan (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapGet("/plans/{planId:guid}/features", async (Guid planId, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var features = await subscriptionService.GetPlanFeaturesAsync(planId);
                return Results.Ok(features);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get plan features";
                op.Description = "Gets all features for a specific plan (Admin only).";
                return op;
            });

            adminSubscriptionApi.MapDelete("/plans/{planId:guid}/features/{featureId:guid}", async (Guid planId, Guid featureId, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!IsAdmin(context)) throw ApiExceptions.Forbidden("Admin access required");
                var removed = await subscriptionService.RemoveFeatureFromPlanAsync(planId, featureId);
                if (!removed)
                    throw ApiExceptions.NotFound("Plan feature not found");
                return Results.Ok(new { message = "Feature removed from plan successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Remove feature from plan";
                op.Description = "Removes a feature from a subscription plan (Admin only).";
                return op;
            });

            // Apple IAP endpoints
            subscriptionApi.MapPost("/apple/subscribe", async (AppleSubscribeRequest dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var sub = await subscriptionService.ActivateAppleSubscriptionAsync(userId, dto);
                return Results.Ok(sub);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Apple IAP subscribe/activate";
                op.Description = "Activates a subscription using Apple In-App Purchase transaction payloads.";
                return op;
            });

            subscriptionApi.MapPost("/apple/restore", async (AppleRestoreRequest dto, ISubscriptionService subscriptionService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var sub = await subscriptionService.RestoreAppleSubscriptionAsync(userId, dto);
                if (sub == null) throw ApiExceptions.NotFound("No subscription could be restored");
                return Results.Ok(sub);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Apple IAP restore";
                op.Description = "Restores a subscription by reconciling with Apple for the current user.";
                return op;
            });

            subscriptionApi.MapPost("/apple/notifications", async (AppleNotificationDto dto, ISubscriptionService subscriptionService, ILogger<Program> logger) =>
            {
                // Apple server notifications do not include user auth; they are signed by Apple.
                logger.LogInformation("Received Apple server notification at /api/subscriptions/apple/notifications. Payload length={Len}", dto.SignedPayload?.Length ?? 0);
                var ok = await subscriptionService.ApplyAppleNotificationAsync(dto);

                if (!ok)
                {
                    logger.LogWarning("Apple server notification processing failed (see SubscriptionService logs for details).");
                }

                // Always return 200 to Apple to avoid excessive retries; internal logs capture failures.
                return Results.Ok(new { status = ok ? "ok" : "processed_with_errors" });
            })
            .WithOpenApi(op => {
                op.Summary = "Apple Server Notifications (ASN v2)";
                op.Description = "Receives signed notifications from Apple and updates subscription state.";
                return op;
            });

            // Minimal API endpoint matching Apple docs: POST /api/apple/webhook with { \"signedPayload\": \"...\" }
            app.MapPost("/api/apple/webhook", async (AppleNotificationDto dto, ISubscriptionService subscriptionService, ILogger<Program> logger) =>
            {
                logger.LogInformation("Received Apple server notification at /api/apple/webhook. Payload length={Len}", dto.SignedPayload?.Length ?? 0);
                var ok = await subscriptionService.ApplyAppleNotificationAsync(dto);

                if (!ok)
                {
                    logger.LogWarning("Apple server notification processing failed (see SubscriptionService logs for details).");
                }

                // Always return 200 to Apple to avoid excessive retries; internal logs capture failures.
                return Results.Ok(new { status = ok ? "ok" : "processed_with_errors" });
            })
            .WithOpenApi(op =>
            {
                op.Summary = "Apple App Store Server Notifications V2 webhook";
                op.Description = "Receives Apple's signedPayload (JWS) at /api/apple/webhook and updates subscription state after verification.";
                return op;
            });
        }
    }
}

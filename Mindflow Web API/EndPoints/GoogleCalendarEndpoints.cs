using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.Services;

namespace Mindflow_Web_API.EndPoints
{
    public static class GoogleCalendarEndpoints
    {
        public static void MapGoogleCalendarEndpoints(this IEndpointRouteBuilder app)
        {
            var calendarApi = app.MapGroup("/api/calendar/google").WithTags("Google Calendar");

            // 1. Connect - returns OAuth URL
            calendarApi.MapPost("/connect", async (IGoogleCalendarService calendarService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var authUrl = await calendarService.BuildConnectUrlAsync(userId);
                return Results.Ok(new { authUrl, success = true });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get Google Calendar OAuth URL";
                op.Description = "Returns the Google OAuth URL for connecting the user's Google Calendar.";
                return op;
            });

            // 2. OAuth callback - called by Google
            calendarApi.MapGet("/callback", async (string code, string state, IGoogleCalendarService calendarService, HttpContext httpContext) =>
            {
                var (success, message) = await calendarService.HandleCallbackAsync(code, state, httpContext);

                var deepLinkBase = "mindflowai://google-calendar/callback";
                string redirectUrl;
                if (success)
                {
                    redirectUrl = $"{deepLinkBase}?success=true&message={Uri.EscapeDataString(message)}";
                }
                else
                {
                    redirectUrl = $"{deepLinkBase}?success=false&error={Uri.EscapeDataString(message)}";
                }

                httpContext.Response.Redirect(redirectUrl);
                return Results.Empty;
            })
            .AllowAnonymous()
            .WithOpenApi(op =>
            {
                op.Summary = "Google OAuth callback";
                op.Description = "Handles Google OAuth callback, stores tokens, and deep-links back into the mobile app.";
                return op;
            });

            // 3. Status
            calendarApi.MapGet("/status", async (IGoogleCalendarService calendarService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var (isConnected, email, lastSyncAt) = await calendarService.GetStatusAsync(userId);
                return Results.Ok(new
                {
                    isConnected,
                    email,
                    lastSyncAt
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get Google Calendar connection status";
                op.Description = "Returns whether Google Calendar is connected, plus email and last sync time.";
                return op;
            });

            // 4. Disconnect
            calendarApi.MapPost("/disconnect", async (IGoogleCalendarService calendarService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                await calendarService.DisconnectAsync(userId);
                return Results.Ok(new { success = true, message = "Google Calendar disconnected successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Disconnect Google Calendar";
                op.Description = "Disconnects the user's Google Calendar and clears stored tokens.";
                return op;
            });

            // 5. Sync (placeholder implementation)
            calendarApi.MapPost("/sync", async (IGoogleCalendarService calendarService, HttpContext context) =>
            {
                    if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var (success, syncedEvents, lastSyncAt) = await calendarService.SyncAsync(userId);
                return Results.Ok(new
                {
                    success,
                    syncedEvents,
                    lastSyncAt
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Sync Google Calendar events";
                op.Description = "Triggers a sync with Google Calendar and returns sync stats.";
                return op;
            });
        }
    }
}



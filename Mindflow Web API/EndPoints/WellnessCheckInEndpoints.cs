using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;

namespace Mindflow_Web_API.EndPoints
{
    public static class WellnessCheckInEndpoints
    {
        public static void MapWellnessCheckInEndpoints(this IEndpointRouteBuilder app)
        {
            var wellnessApi = app.MapGroup("/api/wellness").WithTags("Wellness");

            wellnessApi.MapPatch("/check-in", async (PatchWellnessCheckInDto dto, IWellnessCheckInService wellnessService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var checkIn = await wellnessService.PatchAsync(userId, dto);
                if (checkIn == null)
                    throw ApiExceptions.NotFound("Wellness check-in not found");
                
                return Results.Ok(checkIn);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Patch wellness check-in";
                op.Description = "Partially updates or creates the authenticated user's wellness check-in record. Accepts only the fields to update.";
                return op;
            });

            wellnessApi.MapGet("/check-in", async (IWellnessCheckInService wellnessService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var checkIn = await wellnessService.GetAsync(userId);
                return Results.Ok(checkIn);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get wellness check-in";
                op.Description = "Retrieves the wellness check-in record for the authenticated user.";
                return op;
            });

            // Get wellness summary (for "You're all set!" screen)
            wellnessApi.MapGet("/analysis/summary", async (IWellnessCheckInService wellnessService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var summary = await wellnessService.GetWellnessSummaryAsync(userId);
                return Results.Ok(summary);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get wellness summary";
                op.Description = "Returns a personalized wellness summary for the 'You're all set!' screen with focus areas, self-care frequency, and support needs.";
                return op;
            });

        }
    }
} 
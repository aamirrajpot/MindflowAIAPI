using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;

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
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var checkIn = await wellnessService.PatchAsync(userId, dto);
                if (checkIn == null)
                    return Results.NotFound();
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
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var checkIn = await wellnessService.GetAsync(userId);
                return Results.Ok(checkIn);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get wellness check-in";
                op.Description = "Retrieves the wellness check-in record for the authenticated user.";
                return op;
            });
        }
    }
} 
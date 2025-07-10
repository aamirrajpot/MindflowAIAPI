using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;

namespace Mindflow_Web_API.EndPoints
{
    public static class WellnessCheckInEndpoints
    {
        public static void MapWellnessCheckInEndpoints(this IEndpointRouteBuilder app)
        {
            var wellnessApi = app.MapGroup("/api/wellness").WithTags("Wellness");

            wellnessApi.MapPost("/check-in", async (CreateWellnessCheckInDto dto, IWellnessCheckInService wellnessService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var checkIn = await wellnessService.SubmitAsync(userId, dto);
                return Results.Ok(checkIn);
            }).RequireAuthorization();



            wellnessApi.MapGet("/check-in", async (IWellnessCheckInService wellnessService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var checkIn = await wellnessService.GetAsync(userId);
                return Results.Ok(checkIn);
            }).RequireAuthorization();
        }
    }
} 
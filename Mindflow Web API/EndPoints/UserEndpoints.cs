using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;

namespace Mindflow_Web_API.EndPoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var usersApi = app.MapGroup("/api/users").WithTags("Users");
            usersApi.MapPost("/users/register", async (RegisterUserDto dto, IUserService userService) =>
            {
                var user = await userService.RegisterAsync(dto);
                return Results.Ok(user);
            });

            usersApi.MapPost("/users/signin", async (SignInUserDto dto, IUserService userService) =>
            {
                var tokenResponse = await userService.SignInAsync(dto);
                if (tokenResponse == null)
                    return Results.Unauthorized();
                return Results.Ok(tokenResponse);
            });

            usersApi.MapPost("/users/send-otp", async (string email, IUserService userService) =>
            {
                var result = await userService.SendOtpAsync(email);
                return result.Sent ? Results.Ok(result) : Results.BadRequest(result);
            });

            usersApi.MapPost("/users/verify-otp", async (VerifyOtpDto dto, IUserService userService) =>
            {
                var success = await userService.VerifyOtpAsync(dto);
                return success ? Results.Ok() : Results.BadRequest("Invalid or expired OTP.");
            });

            usersApi.MapPost("/users/change-password", async (ChangePasswordDto dto, IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var success = await userService.ChangePasswordAsync(userId, dto);
                return success ? Results.Ok(success) : Results.BadRequest("Invalid credentials or user not found.");
            }).RequireAuthorization();

            usersApi.MapGet("/users/profile", async (IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var profile = await userService.GetProfileAsync(userId);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            }).RequireAuthorization();



            usersApi.MapPut("/users/profile", async (UpdateProfileDto dto, IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var success = await userService.UpdateProfileAsync(userId, dto);
                return success ? Results.Ok(success) : Results.BadRequest("Profile update failed.");
            }).RequireAuthorization();

            usersApi.MapPost("/users/google-auth", async (GoogleAuthDto dto, IExternalAuthService externalAuthService) =>
            {
                var result = await externalAuthService.GoogleAuthenticateAsync(dto);
                return result is not null ? Results.Ok(result) : Results.Unauthorized();
            });

            usersApi.MapPost("/users/forgot-password", async (ForgotPasswordDto dto, IUserService userService) =>
            {
                var result = await userService.ForgotPasswordAsync(dto);
                return result.Sent ? Results.Ok(result) : Results.BadRequest(result);
            });

            usersApi.MapPost("/users/reset-password", async (ResetPasswordDto dto, IUserService userService) =>
            {
                var success = await userService.ResetPasswordAsync(dto);
                return success ? Results.Ok("Password reset successfully.") : Results.BadRequest("Invalid OTP or user not found.");
            });
        }
    }
} 
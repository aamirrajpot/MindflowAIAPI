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
            })
            .WithOpenApi(op => {
                op.Summary = "Register a new user";
                op.Description = "Creates a new user account with the provided details.";
                return op;
            });

            usersApi.MapPost("/users/signin", async (SignInUserDto dto, IUserService userService) =>
            {
                var tokenResponse = await userService.SignInAsync(dto);
                if (tokenResponse == null)
                    return Results.Unauthorized();
                return Results.Ok(tokenResponse);
            })
            .WithOpenApi(op => {
                op.Summary = "Sign in a user";
                op.Description = "Authenticates a user and returns a JWT token if credentials are valid.";
                return op;
            });

            usersApi.MapPost("/users/send-otp", async (string email, IUserService userService) =>
            {
                var result = await userService.SendOtpAsync(email);
                return result.Sent ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithOpenApi(op => {
                op.Summary = "Send OTP to email";
                op.Description = "Sends a one-time password (OTP) to the specified email address for verification.";
                return op;
            });

            usersApi.MapPost("/users/verify-otp", async (VerifyOtpDto dto, IUserService userService) =>
            {
                var success = await userService.VerifyOtpAsync(dto);
                return success ? Results.Ok() : Results.BadRequest("Invalid or expired OTP.");
            })
            .WithOpenApi(op => {
                op.Summary = "Verify OTP";
                op.Description = "Verifies the OTP sent to the user's email address.";
                return op;
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
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Change user password";
                op.Description = "Allows an authenticated user to change their password.";
                return op;
            });

            usersApi.MapGet("/users/profile", async (IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var profile = await userService.GetProfileAsync(userId);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get user profile";
                op.Description = "Retrieves the profile information of the authenticated user.";
                return op;
            });

            usersApi.MapPut("/users/profile", async (UpdateProfileDto dto, IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                var success = await userService.UpdateProfileAsync(userId, dto);
                return success ? Results.Ok(success) : Results.BadRequest("Profile update failed.");
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Update user profile";
                op.Description = "Updates the profile information of the authenticated user.";
                return op;
            });

            usersApi.MapPost("/users/google-auth", async (GoogleAuthDto dto, IExternalAuthService externalAuthService) =>
            {
                var result = await externalAuthService.GoogleAuthenticateAsync(dto);
                return result is not null ? Results.Ok(result) : Results.Unauthorized();
            })
            .WithOpenApi(op => {
                op.Summary = "Google authentication";
                op.Description = "Authenticates a user using Google OAuth and returns a JWT token if successful.";
                return op;
            });

            usersApi.MapPost("/users/forgot-password", async (ForgotPasswordDto dto, IUserService userService) =>
            {
                var result = await userService.ForgotPasswordAsync(dto);
                return result.Sent ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithOpenApi(op => {
                op.Summary = "Forgot password";
                op.Description = "Sends a password reset OTP to the user's email address.";
                return op;
            });

            usersApi.MapPost("/users/reset-password", async (ResetPasswordDto dto, IUserService userService) =>
            {
                var success = await userService.ResetPasswordAsync(dto);
                return success ? Results.Ok("Password reset successfully.") : Results.BadRequest("Invalid OTP or user not found.");
            })
            .WithOpenApi(op => {
                op.Summary = "Reset password";
                op.Description = "Resets the user's password using the provided OTP and new password.";
                return op;
            });

            usersApi.MapPost("/users/upload-profile-pic", async (HttpContext context, IFormFile file, IUserService userService) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    return Results.Unauthorized();
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Results.Unauthorized();
                try
                {
                    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                    var profilePicUrl = await userService.UploadProfilePictureAsync(userId, file, baseUrl);
                    return Results.Ok(new { profilePicUrl });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (Exception)
                {
                    return Results.StatusCode(500);
                }
            })
            .RequireAuthorization()
            .Accepts<IFormFile>("multipart/form-data", "file")
            .WithOpenApi(op => {
                op.Summary = "Upload profile picture";
                op.Description = "Uploads a profile picture for the user. Only jpg, jpeg, png, gif, and webp formats are allowed. Max size: 2MB.";
                return op;
            })
            .DisableAntiforgery();
        }
    }
} 
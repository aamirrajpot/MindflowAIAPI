using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;

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
                    throw ApiExceptions.Unauthorized("Invalid credentials");
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
                if (!success)
                    throw ApiExceptions.ValidationError("Invalid or expired OTP.");
                return Results.Ok();
            })
            .WithOpenApi(op => {
                op.Summary = "Verify OTP";
                op.Description = "Verifies the OTP sent to the user's email address.";
                return op;
            });

            usersApi.MapPost("/users/change-password", async (ChangePasswordDto dto, IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var success = await userService.ChangePasswordAsync(userId, dto);
                if (!success)
                    throw ApiExceptions.ValidationError("Invalid credentials or user not found.");
                return Results.Ok(success);
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
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var profile = await userService.GetProfileAsync(userId);
                if (profile is null)
                    throw ApiExceptions.NotFound("User profile not found");
                return Results.Ok(profile);
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
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var success = await userService.UpdateProfileAsync(userId, dto);
                if (!success)
                    throw ApiExceptions.ValidationError("Profile update failed.");
                return Results.Ok(success);
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
                if (result is null)
                    throw ApiExceptions.Unauthorized("Google authentication failed");
                return Results.Ok(result);
            })
            .WithOpenApi(op => {
                op.Summary = "Google authentication";
                op.Description = "Authenticates a user using Google OAuth and returns a JWT token if successful.";
                return op;
            });

            usersApi.MapPost("/users/apple-auth", async (AppleAuthDto dto, IExternalAuthService externalAuthService) =>
            {
                var result = await externalAuthService.AppleAuthenticateAsync(dto);
                if (result is null)
                    throw ApiExceptions.Unauthorized("Apple authentication failed");
                return Results.Ok(result);
            })
            .WithOpenApi(op => {
                op.Summary = "Apple authentication";
                op.Description = "Authenticates a user using Apple Sign In and returns a JWT token if successful.";
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
                if (!success)
                    throw ApiExceptions.ValidationError("Invalid OTP or user not found.");
                return Results.Ok("Password reset successfully.");
            })
            .WithOpenApi(op => {
                op.Summary = "Reset password";
                op.Description = "Resets the user's password using the provided OTP and new password.";
                return op;
            });

            usersApi.MapPost("/users/upload-profile-pic", async (HttpContext context, IFormFile file, IUserService userService) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                var profilePicUrl = await userService.UploadProfilePictureAsync(userId, file, baseUrl);
                return Results.Ok(new { profilePicUrl });
            })
            .RequireAuthorization()
            .Accepts<IFormFile>("multipart/form-data", "file")
            .WithOpenApi(op => {
                op.Summary = "Upload profile picture";
                op.Description = "Uploads a profile picture for the user. Only jpg, jpeg, png, gif, and webp formats are allowed. Max size: 2MB.";
                return op;
            })
            .DisableAntiforgery();

            usersApi.MapPost("/users/upload-profile-pic-base64", async (UploadProfilePictureBase64Dto dto, IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                var profilePicUrl = await userService.UploadProfilePictureBase64Async(userId, dto.Base64Image, dto.FileName, baseUrl);
                return Results.Ok(new { profilePicUrl });
            })
            .RequireAuthorization()
            .Accepts<UploadProfilePictureBase64Dto>("application/json")
            .WithOpenApi(op => {
                op.Summary = "Upload profile picture (Base64)";
                op.Description = "Uploads a profile picture using base64 encoded image data. Only jpg, jpeg, png, gif, and webp formats are allowed. Max size: 2MB.";
                return op;
            });

            usersApi.MapPost("/users/upload-profile-pic-url", async (UploadProfilePictureUrlDto dto, IUserService userService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                var profilePicUrl = await userService.UploadProfilePictureFromUrlAsync(userId, dto.ImageUrl, baseUrl);
                return Results.Ok(new { profilePicUrl });
            })
            .RequireAuthorization()
            .Accepts<UploadProfilePictureUrlDto>("application/json")
            .WithOpenApi(op => {
                op.Summary = "Upload profile picture (URL)";
                op.Description = "Downloads and uploads a profile picture from a URL. Only jpg, jpeg, png, gif, and webp formats are allowed. Max size: 2MB.";
                return op;
            });

            // Test endpoint to generate sample base64 image
            usersApi.MapGet("/users/test-base64-image", () =>
            {
                // This is a 1x1 pixel red JPEG image
                var sampleBase64 = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAv/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAAX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCdABmX/9k=";
                
                return Results.Ok(new { 
                    sampleBase64Image = sampleBase64,
                    fileName = "test-image.jpg",
                    description = "This is a 1x1 pixel red JPEG image for testing base64 upload",
                    usage = "Copy the sampleBase64Image value and use it in the base64 upload endpoint"
                });
            })
            .WithOpenApi(op => {
                op.Summary = "Get test base64 image";
                op.Description = "Returns a sample base64 encoded image for testing the base64 upload endpoint.";
                return op;
            });
        }
    }
} 
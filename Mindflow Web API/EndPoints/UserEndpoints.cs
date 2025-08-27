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
            usersApi.MapPost("/register", async (RegisterUserDto dto, IUserService userService) =>
            {
                var user = await userService.RegisterAsync(dto);
                return Results.Ok(user);
            })
            .WithOpenApi(op => {
                op.Summary = "Register a new user";
                op.Description = "Creates a new user account with the provided details.";
                return op;
            });

            usersApi.MapPost("/signin", async (SignInUserDto dto, IUserService userService) =>
            {
                var signInResponse = await userService.SignInAsync(dto);
                if (signInResponse == null)
                    throw ApiExceptions.Unauthorized("Invalid credentials");
                return Results.Ok(signInResponse);
            })
            .WithOpenApi(op => {
                op.Summary = "Sign in a user";
                op.Description = "Authenticates a user and returns a JWT token if credentials are valid.";
                return op;
            });

            usersApi.MapPost("/send-otp", async (string email, IUserService userService) =>
            {
                var result = await userService.SendOtpAsync(email);
                return result.Sent ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithOpenApi(op => {
                op.Summary = "Send OTP to email";
                op.Description = "Sends a one-time password (OTP) to the specified email address for verification.";
                return op;
            });

            usersApi.MapPost("/verify-otp", async (VerifyOtpDto dto, IUserService userService) =>
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

            usersApi.MapPost("/change-password", async (ChangePasswordDto dto, IUserService userService, HttpContext context) =>
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

            usersApi.MapGet("/profile", async (IUserService userService, HttpContext context) =>
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

            usersApi.MapPut("/profile", async (UpdateProfileDto dto, IUserService userService, HttpContext context) =>
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

            usersApi.MapPost("/google-auth", async (GoogleAuthDto dto, IExternalAuthService externalAuthService) =>
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

            usersApi.MapPost("/apple-auth", async (AppleAuthDto dto, IExternalAuthService externalAuthService) =>
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

            usersApi.MapPost("/forgot-password", async (ForgotPasswordDto dto, IUserService userService) =>
            {
                var result = await userService.ForgotPasswordAsync(dto);
                return result.Sent ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithOpenApi(op => {
                op.Summary = "Forgot password";
                op.Description = "Sends a password reset OTP to the user's email address.";
                return op;
            });

            usersApi.MapPost("/reset-password", async (ResetPasswordDto dto, IUserService userService) =>
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

            usersApi.MapPost("/upload-profile-pic", async (HttpContext context, IFormFile file, IUserService userService) =>
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

            usersApi.MapPost("/upload-profile-pic-base64", async (UploadProfilePictureBase64Dto dto, IUserService userService, HttpContext context, ILogger<Program> logger) =>
            {
                logger.LogInformation("🔍 Base64 upload endpoint hit - Request Path: {Path}", context.Request.Path);
                logger.LogInformation("🔍 Request Method: {Method}", context.Request.Method);
                logger.LogInformation("🔍 Content-Type: {ContentType}", context.Request.ContentType);
                
                if (!context.User.Identity?.IsAuthenticated ?? true)
                {
                    logger.LogWarning("❌ User not authenticated");
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                }
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    logger.LogWarning("❌ Invalid user token - UserId: {UserId}", userIdClaim?.Value);
                    throw ApiExceptions.Unauthorized("Invalid user token");
                }
                
                logger.LogInformation("✅ User authenticated - UserId: {UserId}", userId);
                logger.LogInformation("📁 DTO received - FileName: {FileName}, ImageLength: {ImageLength}", 
                    dto.FileName, dto.Base64Image?.Length ?? 0);
                
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                logger.LogInformation("🌐 Base URL constructed: {BaseUrl}", baseUrl);
                
                var profilePicUrl = await userService.UploadProfilePictureBase64Async(userId, dto.Base64Image, dto.FileName, baseUrl);
                logger.LogInformation("✅ Upload successful - ProfilePicUrl: {ProfilePicUrl}", profilePicUrl);
                
                return Results.Ok(new { profilePicUrl });
            })
            .RequireAuthorization()
            .Accepts<UploadProfilePictureBase64Dto>("application/json")
            .WithOpenApi(op => {
                op.Summary = "Upload profile picture (Base64)";
                op.Description = "Uploads a profile picture using base64 encoded image data. Only jpg, jpeg, png, gif, and webp formats are allowed. Max size: 2MB.";
                return op;
            });

            usersApi.MapPost("/upload-profile-pic-url", async (UploadProfilePictureUrlDto dto, IUserService userService, HttpContext context, ILogger<Program> logger) =>
            {
                logger.LogInformation("🔍 URL upload endpoint hit - Request Path: {Path}", context.Request.Path);
                logger.LogInformation("🔍 Request Method: {Method}", context.Request.Method);
                logger.LogInformation("🔍 Content-Type: {ContentType}", context.Request.ContentType);
                
                if (!context.User.Identity?.IsAuthenticated ?? true)
                {
                    logger.LogWarning("❌ User not authenticated");
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                }
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    logger.LogWarning("❌ Invalid user token - UserId: {UserId}", userIdClaim?.Value);
                    throw ApiExceptions.Unauthorized("Invalid user token");
                }
                
                logger.LogInformation("✅ User authenticated - UserId: {UserId}", userId);
                logger.LogInformation("📁 DTO received - ImageUrl: {ImageUrl}", dto.ImageUrl);
                
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                logger.LogInformation("🌐 Base URL constructed: {BaseUrl}", baseUrl);
                
                var profilePicUrl = await userService.UploadProfilePictureFromUrlAsync(userId, dto.ImageUrl, baseUrl);
                logger.LogInformation("✅ Upload successful - ProfilePicUrl: {ProfilePicUrl}", profilePicUrl);
                
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
            usersApi.MapGet("/test-base64-image", () =>
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

            // Authorized endpoint to test Azure secrets configuration
            usersApi.MapGet("/test-azure-secrets", (IConfiguration configuration, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                // Check if user is admin
                var isAdmin = context.User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Admin");
                if (!isAdmin)
                    throw ApiExceptions.Forbidden("Only administrators can access this endpoint");
                
                // Read secrets from configuration (Azure Web App Application Settings)
                var stripeSecretKey = configuration["Stripe:SecretKey"];
                var stripePublishableKey = configuration["Stripe:PublishableKey"];
                var stripeWebhookSecret = configuration["Stripe:WebhookSecret"];
                var jwtKey = configuration["Jwt:Key"];
                var emailSmtpPassword = configuration["Email:SenderPassword"];
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                
                // Create a safe response that masks sensitive data
                var response = new
                {
                    timestamp = DateTime.UtcNow,
                    userId = userId,
                    isAdmin = isAdmin,
                    configurationStatus = new
                    {
                        stripeSecretKey = !string.IsNullOrEmpty(stripeSecretKey) ? $"{stripeSecretKey.Substring(0, Math.Min(7, stripeSecretKey.Length))}..." : "NOT_SET",
                        stripePublishableKey = !string.IsNullOrEmpty(stripePublishableKey) ? $"{stripePublishableKey.Substring(0, Math.Min(7, stripePublishableKey.Length))}..." : "NOT_SET",
                        stripeWebhookSecret = !string.IsNullOrEmpty(stripeWebhookSecret) ? $"{stripeWebhookSecret.Substring(0, Math.Min(7, stripeWebhookSecret.Length))}..." : "NOT_SET",
                        jwtKey = !string.IsNullOrEmpty(jwtKey) ? $"{jwtKey.Substring(0, Math.Min(7, jwtKey.Length))}..." : "NOT_SET",
                        emailSmtpPassword = !string.IsNullOrEmpty(emailSmtpPassword) ? "SET" : "NOT_SET",
                        connectionString = !string.IsNullOrEmpty(connectionString) ? "SET" : "NOT_SET"
                    },
                    secretsFound = new
                    {
                        stripeSecretKey = !string.IsNullOrEmpty(stripeSecretKey),
                        stripePublishableKey = !string.IsNullOrEmpty(stripePublishableKey),
                        stripeWebhookSecret = !string.IsNullOrEmpty(stripeWebhookSecret),
                        jwtKey = !string.IsNullOrEmpty(jwtKey),
                        emailSmtpPassword = !string.IsNullOrEmpty(emailSmtpPassword),
                        connectionString = !string.IsNullOrEmpty(connectionString)
                    },
                    totalSecretsConfigured = new[]
                    {
                        !string.IsNullOrEmpty(stripeSecretKey),
                        !string.IsNullOrEmpty(stripePublishableKey),
                        !string.IsNullOrEmpty(stripeWebhookSecret),
                        !string.IsNullOrEmpty(jwtKey),
                        !string.IsNullOrEmpty(emailSmtpPassword),
                        !string.IsNullOrEmpty(connectionString)
                    }.Count(x => x),
                    message = "Azure Web App secrets configuration test completed successfully"
                };
                
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Test Azure secrets configuration";
                op.Description = "Authorized endpoint for administrators to test if Azure Web App Application Settings are properly configured. Returns masked values of secrets for security.";
                return op;
            });

            // Simple test endpoint to verify routing
            usersApi.MapGet("/test-routing", (ILogger<Program> logger) =>
            {
                logger.LogInformation("🎯 Test routing endpoint hit successfully!");
                return Results.Ok(new { 
                    message = "Routing is working!",
                    timestamp = DateTime.UtcNow,
                    endpoints = new[] {
                        "/api/users/upload-profile-pic-base64",
                        "/api/users/upload-profile-pic-url",
                        "/api/users/upload-profile-pic"
                    }
                });
            })
            .WithOpenApi(op => {
                op.Summary = "Test routing";
                op.Description = "Simple endpoint to verify that routing is working correctly.";
                return op;
            });
        }
    }
} 
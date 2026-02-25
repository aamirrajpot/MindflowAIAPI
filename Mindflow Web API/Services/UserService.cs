using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Exceptions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Mindflow_Web_API.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Mindflow_Web_API.Services
{
    public class UserService : IUserService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        private readonly ISubscriptionService _subscriptionService;

        public UserService(MindflowDbContext dbContext, ILogger<UserService> logger, IConfiguration configuration, IEmailService emailService, ISubscriptionService subscriptionService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
            _subscriptionService = subscriptionService;
        }

        public async Task<UserDto> RegisterAsync(RegisterUserDto command)
        {
            // Check for existing active users
            if (await _dbContext.Users.AnyAsync(u => (u.UserName == command.UserName || u.Email == command.Email) && u.IsActive))
                throw ApiExceptions.Conflict("Username or Email already exists.");
            
            // Check for deactivated users with same email/username
            var unconfirmedUser = await _dbContext.Users.FirstOrDefaultAsync(u => 
                (u.UserName == command.UserName || u.Email == command.Email) && !u.IsActive && !u.EmailConfirmed);
            
            if (unconfirmedUser != null)
            {
                // Automatically send OTP after registration
                await SendOtpAsync(unconfirmedUser.Email);
                return new UserDto(unconfirmedUser.Id, unconfirmedUser.UserName, unconfirmedUser.Email, unconfirmedUser.EmailConfirmed, unconfirmedUser.FirstName, unconfirmedUser.LastName, unconfirmedUser.IsActive, unconfirmedUser.DateOfBirth, unconfirmedUser.ProfilePic, unconfirmedUser.StripeCustomerId, unconfirmedUser.QuestionnaireFilled);
            }

            var user = new User
            {
                UserName = command.UserName,
                Email = command.Email,
                FirstName = command.FirstName,
                LastName = command.LastName,
                PasswordHash = PasswordHelper.HashPassword(command.Password),
                SecurityStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = false,
                IsActive = false
            };

            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"User registered: {user.UserName}");

            // Automatically send OTP after registration
            await SendOtpAsync(user.Email);

            return new UserDto(user.Id, user.UserName, user.Email, user.EmailConfirmed, user.FirstName, user.LastName, user.IsActive, user.DateOfBirth, user.ProfilePic, user.StripeCustomerId, user.QuestionnaireFilled);
        }

        public async Task<SignInResponseDto?> SignInAsync(SignInUserDto command)
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedInput = command.UserNameOrEmail?.ToLowerInvariant() ?? string.Empty;
            var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
                (u.UserName != null && u.UserName.ToLower() == normalizedInput) || 
                (u.Email != null && u.Email.ToLower() == normalizedInput));
            if (user == null || !PasswordHelper.VerifyPassword(command.Password, user.PasswordHash))
            {
                _logger.LogWarning($"Failed sign-in attempt for: {command.UserNameOrEmail}");
                return null;
            }
            if (!user.IsActive || !user.EmailConfirmed)
            {
                _logger.LogWarning($"Inactive or unconfirmed user tried to sign in: {user.UserName}");
                throw ApiExceptions.ValidationError("Please verify your email and then try to sign in.");
            }
            // Generate JWT token
            int expiresInSeconds;
            var tokenString = JwtHelper.GenerateJwtToken(user, _configuration, out expiresInSeconds);
            
            // Generate refresh token
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiresDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiresInDays", 30);
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiresDays),
                IsRevoked = false
            };
            
            // Revoke old refresh tokens for this user (optional: keep last N tokens)
            var oldTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();
            foreach (var oldToken in oldTokens)
            {
                oldToken.IsRevoked = true;
                oldToken.RevokedAt = DateTime.UtcNow;
                oldToken.ReplacedByToken = refreshToken;
            }
            
            await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"User signed in: {user.UserName}");
            
            var userDto = new UserDto(user.Id, user.UserName, user.Email, user.EmailConfirmed, user.FirstName, user.LastName, user.IsActive, user.DateOfBirth, user.ProfilePic, user.StripeCustomerId, user.QuestionnaireFilled);
            var appAccountToken = await _subscriptionService.CreateAppleAppAccountTokenAsync(user.Id);
            return new SignInResponseDto(tokenString, "Bearer", expiresInSeconds, refreshToken, userDto, appAccountToken);
        }
        
        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }
        
        public async Task<RefreshTokenResponseDto?> RefreshTokenAsync(string refreshToken)
        {
            var tokenEntity = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);
            
            if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Invalid or expired refresh token attempted.");
                return null;
            }
            
            var user = await _dbContext.Users.FindAsync(tokenEntity.UserId);
            if (user == null || !user.IsActive || !user.EmailConfirmed)
            {
                _logger.LogWarning("User associated with refresh token is inactive or unconfirmed.");
                return null;
            }
            
            // Generate new access token
            int expiresInSeconds;
            var newAccessToken = JwtHelper.GenerateJwtToken(user, _configuration, out expiresInSeconds);
            
            // Generate new refresh token (token rotation)
            var newRefreshToken = GenerateRefreshToken();
            var refreshTokenExpiresDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiresInDays", 30);
            var newRefreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiresDays),
                IsRevoked = false
            };
            
            // Revoke old refresh token
            tokenEntity.IsRevoked = true;
            tokenEntity.RevokedAt = DateTime.UtcNow;
            tokenEntity.ReplacedByToken = newRefreshToken;
            
            await _dbContext.RefreshTokens.AddAsync(newRefreshTokenEntity);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"Refresh token used for user {user.UserName}. New tokens issued.");
            
            return new RefreshTokenResponseDto(newAccessToken, "Bearer", expiresInSeconds, newRefreshToken);
        }

        public async Task<SendOtpResponseDto> SendOtpAsync(string email)
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = email?.ToLowerInvariant() ?? string.Empty;
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            if (user == null)
                return new SendOtpResponseDto(email, false);

            // Mark all previous OTPs as expired
            var previousOtps = await _dbContext.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed)
                .ToListAsync();
            foreach (var otpRecord in previousOtps)
            {
                otpRecord.IsUsed = true; // Mark as used/expired
            }

            // Generate OTP
            var otp = new Random().Next(1000, 10000).ToString();
            var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
            var userOtp = new UserOtp
            {
                UserId = user.Id,
                Otp = otp,
                Expiry = expiry,
                IsUsed = false
            };
            await _dbContext.UserOtps.AddAsync(userOtp);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"Sending OTP email to {email} for user {user.UserName}");
            
            var subject = "Your Mindflow AI OTP Code";
            var body = $@"
                    Hi {user.FirstName} {user.LastName},<br/><br/>
                    Welcome to <b>Mindflow AI</b>! To complete your registration, please use the following One-Time Password (OTP):<br/><br/>
                    <h2 style='color:#2e86de;'>🔐 {otp}</h2><br/>
                    This code is valid for the next <b>5 minutes</b>.<br/><br/>
                    If you did not request this, you can safely ignore this email.<br/><br/>
                    Thanks,<br/>
                    <b>Mindflow AI Team</b>
                    ";
            var sent = await _emailService.SendEmailAsync(email, subject, body, true);
            return new SendOtpResponseDto(email, sent);
        }

        public async Task<bool> VerifyOtpAsync(VerifyOtpDto command)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == command.UserId);
            if (user == null)
                return false;

            // Get the latest non-expired OTP using raw SQL
            var currentTime = DateTimeOffset.UtcNow;
            var otpRecord = await _dbContext.UserOtps
                .FromSqlRaw(@"
                    SELECT * FROM UserOtps 
                    WHERE UserId = {0} 
                    AND Otp = {1} 
                    AND IsUsed = 0 
                    AND Expiry > {2}
                    ORDER BY Expiry DESC 
                    LIMIT 1", user.Id, command.Code, currentTime)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
                return false;

            otpRecord.IsUsed = true;
            user.IsActive = true;
            user.EmailConfirmed = true;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto command)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return false;
            if (!PasswordHelper.VerifyPassword(command.CurrentPassword, user.PasswordHash))
                return false;
            user.PasswordHash = PasswordHelper.HashPassword(command.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Password changed for user: {user.UserName}");
            return true;
        }

        public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return null;

            var activeSubscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
            var appAccountToken = await _subscriptionService.CreateAppleAppAccountTokenAsync(userId);

            return new UserProfileDto(
                user.UserName,
                user.Email,
                user.FirstName,
                user.LastName,
                user.DateOfBirth,
                user.ProfilePic,
                user.QuestionnaireFilled,
                activeSubscription,
                appAccountToken);
        }

        public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDto command)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return false;
            user.FirstName = command.FirstName;
            user.LastName = command.LastName;
            user.DateOfBirth = command.DateOfBirth;
            if (command.ProfilePic != null)
                user.ProfilePic = command.ProfilePic;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Profile updated for user: {user.UserName}");
            return true;
        }

        public async Task<SendOtpResponseDto> ForgotPasswordAsync(ForgotPasswordDto command)
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = command.Email?.ToLowerInvariant() ?? string.Empty;
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            if (user == null)
                return new SendOtpResponseDto(command.Email, false);

            // Mark all previous OTPs as expired
            var previousOtps = await _dbContext.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed)
                .ToListAsync();
            foreach (var otpRecord in previousOtps)
            {
                otpRecord.IsUsed = true; // Mark as used/expired
            }

            // Generate OTP
            var otp = new Random().Next(1000, 10000).ToString();
            var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
            var userOtp = new UserOtp
            {
                UserId = user.Id,
                Otp = otp,
                Expiry = expiry,
                IsUsed = false
            };
            await _dbContext.UserOtps.AddAsync(userOtp);
            await _dbContext.SaveChangesAsync();

            var subject = "Password Reset - Mindflow AI";
            var body = $@"
                    Hi {user.FirstName} {user.LastName},<br/><br/>
                    You requested a password reset for your <b>Mindflow AI</b> account. Please use the following One-Time Password (OTP) to reset your password:<br/><br/>
                    <h2 style='color:#e74c3c;'>🔐 {otp}</h2><br/>
                    This code is valid for the next <b>5 minutes</b>.<br/><br/>
                    If you did not request this password reset, please ignore this email and your password will remain unchanged.<br/><br/>
                    Thanks,<br/>
                    <b>Mindflow AI Team</b>
                    ";
            var sent = await _emailService.SendEmailAsync(command.Email, subject, body, true);
            return new SendOtpResponseDto(command.Email, sent);
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordDto command)
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = command.Email?.ToLowerInvariant() ?? string.Empty;
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            if (user == null)
                return false;

            // Get the latest non-expired OTP using raw SQL
            var currentTime = DateTimeOffset.UtcNow;
            var otpRecord = await _dbContext.UserOtps
                .FromSqlRaw(@"
                    SELECT * FROM UserOtps 
                    WHERE UserId = {0} 
                    AND Otp = {1} 
                    AND IsUsed = 0 
                    AND Expiry > {2}
                    ORDER BY Expiry DESC 
                    LIMIT 1", user.Id, command.Otp, currentTime)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
                return false;

            otpRecord.IsUsed = true;
            user.PasswordHash = PasswordHelper.HashPassword(command.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Password reset for user: {user.UserName}");
            return true;
        }

        public async Task<string> UploadProfilePictureAsync(Guid userId, IFormFile file, string baseUrl)
        {
            if (file == null || file.Length == 0)
                throw ApiExceptions.ValidationError("No file uploaded.");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw ApiExceptions.ValidationError("Invalid file type. Only jpg, jpeg, png, gif, and webp are allowed.");

            // Validate file size (max 2MB)
            const long maxFileSize = 2 * 1024 * 1024; // 2MB
            if (file.Length > maxFileSize)
                throw ApiExceptions.ValidationError("File size exceeds 2MB limit.");

            var uploads = Path.Combine("wwwroot", "profilepics");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            // Find user and delete old profile pic if exists
            var user = await _dbContext.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.ProfilePic))
            {
                try
                {
                    var oldPicUrl = user.ProfilePic;
                    // Only delete if the old pic is in our uploads folder
                    var uploadsUrl = $"{baseUrl}/profilepics/";
                    if (oldPicUrl.StartsWith(uploadsUrl))
                    {
                        var oldFileName = oldPicUrl.Substring(uploadsUrl.Length);
                        var oldFilePath = Path.Combine(uploads, oldFileName);
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }
                }
                catch { /* Ignore file deletion errors */ }
            }

            var sanitizedFileName = SanitizeFileName(file.FileName);
            var fileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
            var filePath = Path.Combine(uploads, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            var profilePicUrl = $"{baseUrl}/profilepics/{fileName}";

            // Save to DB
            if (user != null)
            {
                user.ProfilePic = profilePicUrl;
                await _dbContext.SaveChangesAsync();
            }
            return profilePicUrl;
        }

        public async Task<string> UploadProfilePictureBase64Async(Guid userId, string base64Image, string fileName, string baseUrl)
        {
            _logger.LogInformation("🚀 Starting base64 upload - UserId: {UserId}, FileName: {FileName}", userId, fileName);
            
            if (string.IsNullOrEmpty(base64Image))
            {
                _logger.LogError("❌ No image data provided");
                throw ApiExceptions.ValidationError("No image data provided.");
            }

            _logger.LogInformation("📝 Processing base64 data - Length: {Length}", base64Image.Length);

            // Remove data URL prefix if present
            var imageData = base64Image;
            if (base64Image.StartsWith("data:image/"))
            {
                var commaIndex = base64Image.IndexOf(',');
                if (commaIndex > 0)
                {
                    imageData = base64Image.Substring(commaIndex + 1);
                    _logger.LogInformation("🔧 Removed data URL prefix - New length: {Length}", imageData.Length);
                }
            }

            // Validate base64 string
            try
            {
                Convert.FromBase64String(imageData);
                _logger.LogInformation("✅ Base64 validation passed");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Invalid base64 image data: {Error}", ex.Message);
                throw ApiExceptions.ValidationError("Invalid base64 image data.");
            }

            // Validate file type from base64 header or filename
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogError("❌ Invalid file type: {Extension}", extension);
                throw ApiExceptions.ValidationError("Invalid file type. Only jpg, jpeg, png, gif, and webp are allowed.");
            }

            _logger.LogInformation("✅ File type validation passed - Extension: {Extension}", extension);

            // Validate file size (max 2MB)
            const long maxFileSize = 2 * 1024 * 1024; // 2MB
            var imageBytes = Convert.FromBase64String(imageData);
            if (imageBytes.Length > maxFileSize)
            {
                _logger.LogError("❌ File size exceeds limit - Size: {Size} bytes, Max: {MaxSize} bytes", imageBytes.Length, maxFileSize);
                throw ApiExceptions.ValidationError("File size exceeds 2MB limit.");
            }

            _logger.LogInformation("✅ File size validation passed - Size: {Size} bytes", imageBytes.Length);

            var uploads = Path.Combine("wwwroot", "profilepics");
            if (!Directory.Exists(uploads))
            {
                Directory.CreateDirectory(uploads);
                _logger.LogInformation("📁 Created uploads directory: {Directory}", uploads);
            }

            // Find user and delete old profile pic if exists
            var user = await _dbContext.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.ProfilePic))
            {
                try
                {
                    var oldPicUrl = user.ProfilePic;
                    var uploadsUrl = $"{baseUrl}/profilepics/";
                    if (oldPicUrl.StartsWith(uploadsUrl))
                    {
                        var oldFileName = oldPicUrl.Substring(uploadsUrl.Length);
                        var oldFilePath = Path.Combine(uploads, oldFileName);
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                            _logger.LogInformation("🗑️ Deleted old profile picture: {FilePath}", oldFilePath);
                        }
                    }
                }
                catch (Exception ex) 
                { 
                    _logger.LogWarning("⚠️ Failed to delete old profile picture: {Error}", ex.Message);
                }
            }

            var sanitizedFileName = SanitizeFileName(fileName);
            var newFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
            var filePath = Path.Combine(uploads, newFileName);
            
            _logger.LogInformation("💾 Saving file - Path: {FilePath}, FileName: {FileName}", filePath, newFileName);
            
            await File.WriteAllBytesAsync(filePath, imageBytes);
            
            var profilePicUrl = $"{baseUrl}/profilepics/{newFileName}";
            _logger.LogInformation("🔗 Generated profile pic URL: {Url}", profilePicUrl);

            // Save to DB
            if (user != null)
            {
                user.ProfilePic = profilePicUrl;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("💾 Updated user profile in database - UserId: {UserId}", userId);
            }
            
            _logger.LogInformation("✅ Base64 upload completed successfully");
            return profilePicUrl;
        }

        public async Task<string> UploadProfilePictureFromUrlAsync(Guid userId, string imageUrl, string baseUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                throw ApiExceptions.ValidationError("No image URL provided.");

            // Validate URL
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                throw ApiExceptions.ValidationError("Invalid image URL.");

            var uploads = Path.Combine("wwwroot", "profilepics");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            // Find user and delete old profile pic if exists
            var user = await _dbContext.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.ProfilePic))
            {
                try
                {
                    var oldPicUrl = user.ProfilePic;
                    var uploadsUrl = $"{baseUrl}/profilepics/";
                    if (oldPicUrl.StartsWith(uploadsUrl))
                    {
                        var oldFileName = oldPicUrl.Substring(uploadsUrl.Length);
                        var oldFilePath = Path.Combine(uploads, oldFileName);
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }
                }
                catch { /* Ignore file deletion errors */ }
            }

            // Download image from URL
            using var httpClient = new HttpClient();
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
            
            // Validate file size (max 2MB)
            const long maxFileSize = 2 * 1024 * 1024; // 2MB
            if (imageBytes.Length > maxFileSize)
                throw ApiExceptions.ValidationError("File size exceeds 2MB limit.");

            // Determine file extension from URL or content type
            var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
            {
                // Try to get from content type
                var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
                var contentType = response.Content.Headers.ContentType?.MediaType;
                extension = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg" // Default
                };
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(extension))
                throw ApiExceptions.ValidationError("Invalid file type. Only jpg, jpeg, png, gif, and webp are allowed.");

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploads, fileName);
            await File.WriteAllBytesAsync(filePath, imageBytes);
            
            var profilePicUrl = $"{baseUrl}/profilepics/{fileName}";

            // Save to DB
            if (user != null)
            {
                user.ProfilePic = profilePicUrl;
                await _dbContext.SaveChangesAsync();
            }
            return profilePicUrl;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "image.jpg";

            // Remove or replace problematic characters
            var sanitized = fileName
                .Replace(" ", "_")           // Replace spaces with underscores
                .Replace("(", "")            // Remove parentheses
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "")
                .Replace("\\", "")
                .Replace("/", "")
                .Replace(":", "")
                .Replace("*", "")
                .Replace("?", "")
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("&", "and")
                .Replace("+", "plus")
                .Replace("=", "equals")
                .Replace(";", "")
                .Replace(",", "")
                .Replace("!", "")
                .Replace("@", "at")
                .Replace("#", "hash")
                .Replace("$", "")
                .Replace("%", "percent")
                .Replace("^", "")
                .Replace("~", "")
                .Replace("`", "")
                .Replace("'", "")
                .Replace("\"", "");

            // Ensure it has a valid extension
            var extension = Path.GetExtension(sanitized).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
            {
                sanitized += ".jpg"; // Default extension
            }

            // Limit length to avoid filesystem issues
            if (sanitized.Length > 100)
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
                var ext = Path.GetExtension(sanitized);
                sanitized = nameWithoutExt.Substring(0, 100 - ext.Length) + ext;
            }

            return sanitized;
        }

        public async Task<bool> DeactivateAccountAsync(Guid userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return false;

            try
            {
                // Mark user as deactivated immediately
                user.DeactivatedAtUtc = DateTime.UtcNow;
                user.IsActive = false;
                await _dbContext.SaveChangesAsync();
    
                // Delete user data immediately (synchronously)
                await DeleteUserDataAsync(_dbContext, userId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate user {UserId}", userId);
                return false;
            }
        }


        private async Task DeleteUserDataAsync(MindflowDbContext dbContext, Guid userId)
        {
            _logger.LogInformation("🗑️ Deleting all data for user {UserId}", userId);

            // Delete in order to respect foreign key constraints

            // 1. Delete BrainDumpEntries
            _logger.LogInformation("🗑️ Querying brain dump entries for user {UserId}", userId);
            var brainDumpEntries = await dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId)
                .ToListAsync();
            if (brainDumpEntries.Any())
            {
                dbContext.BrainDumpEntries.RemoveRange(brainDumpEntries);
                _logger.LogInformation("🗑️ Deleted {Count} brain dump entries", brainDumpEntries.Count);
            }

            // 2. Delete TaskItems
            _logger.LogInformation("🗑️ Querying task items for user {UserId}", userId);
            var taskItems = await dbContext.Tasks
                .Where(t => t.UserId == userId)
                .ToListAsync();
            if (taskItems.Any())
            {
                dbContext.Tasks.RemoveRange(taskItems);
                _logger.LogInformation("🗑️ Deleted {Count} task items", taskItems.Count);
            }

            // 3. Delete WellnessCheckIns
            _logger.LogInformation("🗑️ Querying wellness check-ins for user {UserId}", userId);
            var wellnessCheckIns = await dbContext.WellnessCheckIns
                .Where(w => w.UserId == userId)
                .ToListAsync();
            if (wellnessCheckIns.Any())
            {
                dbContext.WellnessCheckIns.RemoveRange(wellnessCheckIns);
                _logger.LogInformation("🗑️ Deleted {Count} wellness check-ins", wellnessCheckIns.Count);
            }

            // 4. Delete UserSubscriptions
            _logger.LogInformation("🗑️ Querying user subscriptions for user {UserId}", userId);
            var userSubscriptions = await dbContext.UserSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync();
            if (userSubscriptions.Any())
            {
                dbContext.UserSubscriptions.RemoveRange(userSubscriptions);
                _logger.LogInformation("🗑️ Deleted {Count} user subscriptions", userSubscriptions.Count);
            }

            // 5. Delete PaymentHistory
            _logger.LogInformation("🗑️ Querying payment history for user {UserId}", userId);
            var paymentHistories = await dbContext.PaymentHistory
                .Where(p => p.UserId == userId)
                .ToListAsync();
            if (paymentHistories.Any())
            {
                dbContext.PaymentHistory.RemoveRange(paymentHistories);
                _logger.LogInformation("🗑️ Deleted {Count} payment histories", paymentHistories.Count);
            }

            // 6. Delete PaymentCards
            _logger.LogInformation("🗑️ Querying payment cards for user {UserId}", userId);
            var paymentCards = await dbContext.PaymentCards
                .Where(p => p.UserId == userId)
                .ToListAsync();
            if (paymentCards.Any())
            {
                dbContext.PaymentCards.RemoveRange(paymentCards);
                _logger.LogInformation("🗑️ Deleted {Count} payment cards", paymentCards.Count);
            }

            // 7. Delete UserOTPs
            _logger.LogInformation("🗑️ Querying user OTPs for user {UserId}", userId);
            var userOtps = await dbContext.UserOtps
                .Where(o => o.UserId == userId)
                .ToListAsync();
            if (userOtps.Any())
            {
                dbContext.UserOtps.RemoveRange(userOtps);
                _logger.LogInformation("🗑️ Deleted {Count} user OTPs", userOtps.Count);
            }

            // 8. Finally, delete the User
            _logger.LogInformation("🗑️ Querying user record for user {UserId}", userId);
            var user = await dbContext.Users.FindAsync(userId);
            if (user != null)
            {
                dbContext.Users.Remove(user);
                _logger.LogInformation("🗑️ Deleted user record");
            }

            // Save all changes
            _logger.LogInformation("💾 Saving all deletions to database for user {UserId}", userId);
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("💾 All deletions saved to database for user {UserId}", userId);
        }
    }
}
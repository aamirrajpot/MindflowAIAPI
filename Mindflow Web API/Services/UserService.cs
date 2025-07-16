using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Services
{
    public class UserService : IUserService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public UserService(MindflowDbContext dbContext, ILogger<UserService> logger, IConfiguration configuration, IEmailService emailService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<UserDto> RegisterAsync(RegisterUserDto command)
        {
            if (await _dbContext.Users.AnyAsync(u => u.UserName == command.UserName || u.Email == command.Email))
                throw new InvalidOperationException("Username or Email already exists.");

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

            return new UserDto(user.Id, user.UserName, user.Email, user.EmailConfirmed, user.FirstName, user.LastName, user.IsActive, user.DateOfBirth, user.ProfilePic);
        }

        public async Task<TokenResponseDto?> SignInAsync(SignInUserDto command)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
                u.UserName == command.UserNameOrEmail || u.Email == command.UserNameOrEmail);
            if (user == null || !PasswordHelper.VerifyPassword(command.Password, user.PasswordHash))
            {
                _logger.LogWarning($"Failed sign-in attempt for: {command.UserNameOrEmail}");
                return null;
            }
            if (!user.IsActive || !user.EmailConfirmed)
            {
                _logger.LogWarning($"Inactive or unconfirmed user tried to sign in: {user.UserName}");
                throw new InvalidOperationException("Please verify your email and then try to sign in.");
            }
            // Generate JWT token
            int expiresInSeconds;
            var tokenString = JwtHelper.GenerateJwtToken(user, _configuration, out expiresInSeconds);
            _logger.LogInformation($"User signed in: {user.UserName}");
            return new TokenResponseDto(tokenString, "Bearer", expiresInSeconds);
        }

        public async Task<SendOtpResponseDto> SendOtpAsync(string email)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
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

            // Get the latest non-expired OTP
            var otpRecord = await _dbContext.UserOtps
                .Where(o => o.UserId == user.Id && o.Otp == command.Code && !o.IsUsed && o.Expiry > DateTimeOffset.UtcNow)
                .OrderByDescending(o => o.Expiry) // Get the latest one
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
            return new UserProfileDto(user.UserName, user.Email, user.FirstName, user.LastName, user.DateOfBirth, user.ProfilePic);
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
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == command.Email);
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
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == command.Email);
            if (user == null)
                return false;

            // Get the latest non-expired OTP
            var otpRecord = await _dbContext.UserOtps
                .Where(o => o.UserId == user.Id && o.Otp == command.Otp && !o.IsUsed && o.Expiry > DateTimeOffset.UtcNow)
                .OrderByDescending(o => o.Expiry) // Get the latest one
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
                throw new ArgumentException("No file uploaded.");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException("Invalid file type. Only jpg, jpeg, png, gif, and webp are allowed.");

            // Validate file size (max 2MB)
            const long maxFileSize = 2 * 1024 * 1024; // 2MB
            if (file.Length > maxFileSize)
                throw new ArgumentException("File size exceeds 2MB limit.");

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

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
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
    }
}
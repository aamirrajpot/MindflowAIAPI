using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
                PasswordHash = HashPassword(command.Password),
                SecurityStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = false,
                IsActive = false
            };

            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"User registered: {user.UserName}");

            // Automatically send OTP after registration
            await SendOtpAsync(user.Email);

            return new UserDto(user.Id, user.UserName, user.Email, user.EmailConfirmed, user.FirstName, user.LastName, user.IsActive, user.DateOfBirth);
        }

        public async Task<TokenResponseDto?> SignInAsync(SignInUserDto command)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
                u.UserName == command.UserNameOrEmail || u.Email == command.UserNameOrEmail);
            if (user == null || !VerifyPassword(command.Password, user.PasswordHash))
            {
                _logger.LogWarning($"Failed sign-in attempt for: {command.UserNameOrEmail}");
                return null;
            }
            if (!user.IsActive)
            {
                _logger.LogWarning($"Inactive user tried to sign in: {user.UserName}");
                return null;
            }
            // Generate JWT token
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresInMinutes = double.Parse(jwtSettings["ExpiresInMinutes"]!);
            var expires = DateTime.UtcNow.AddMinutes(expiresInMinutes);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            };
            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            _logger.LogInformation($"User signed in: {user.UserName}");
            return new TokenResponseDto(tokenString, "Bearer", (int)(expiresInMinutes * 60));
        }

        public async Task<SendOtpResponseDto> SendOtpAsync(string email)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return new SendOtpResponseDto(email, false);
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
            var otpRecord = await _dbContext.UserOtps
                .Where(o => o.UserId == user.Id && o.Otp == command.Code && !o.IsUsed && o.Expiry > DateTimeOffset.UtcNow)
                .OrderByDescending(o => o.Expiry)
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
            if (!VerifyPassword(command.CurrentPassword, user.PasswordHash))
                return false;
            user.PasswordHash = HashPassword(command.NewPassword);
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
            return new UserProfileDto(user.UserName, user.Email, user.FirstName, user.LastName, user.DateOfBirth);
        }

        public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDto command)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return false;
            user.FirstName = command.FirstName;
            user.LastName = command.LastName;
            user.DateOfBirth = command.DateOfBirth;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Profile updated for user: {user.UserName}");
            return true;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
} 
using Google.Apis.Auth;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Mindflow_Web_API.Services
{
    public class ExternalAuthService : IExternalAuthService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExternalAuthService> _logger;

        public ExternalAuthService(MindflowDbContext dbContext, IConfiguration configuration, ILogger<ExternalAuthService> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ExternalAuthResponseDto?> GoogleAuthenticateAsync(GoogleAuthDto command)
        {
            GoogleJsonWebSignature.Payload? payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(command.IdToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Invalid Google ID token: {ex.Message}");
                return null;
            }

            // Check if user exists by email
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
            bool isNewUser = false;
            if (user == null)
            {
                // Create new user
                user = new User
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    FirstName = payload.GivenName ?? string.Empty,
                    LastName = payload.FamilyName ?? string.Empty,
                    EmailConfirmed = true,
                    IsActive = true,
                    PasswordHash = string.Empty, // No password for external
                    SecurityStamp = Guid.NewGuid().ToString()
                };
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();
                isNewUser = true;
            }

            // Generate JWT
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
            return new ExternalAuthResponseDto(tokenString, "Bearer", (int)(expiresInMinutes * 60), isNewUser);
        }
    }
} 
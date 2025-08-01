using Google.Apis.Auth;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mindflow_Web_API.Utilities;

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
                user = User.Create(
                    userName: payload.Email,
                    email: payload.Email,
                    firstName: payload.GivenName ?? string.Empty,
                    lastName: payload.FamilyName ?? string.Empty,
                    emailConfirmed: true,
                    isActive: true,
                    passwordHash: string.Empty, // No password for external
                    securityStamp: Guid.NewGuid().ToString(),
                    sub: null
                );
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();
                isNewUser = true;
            }

            // Generate JWT
            int expiresInSeconds;
            var tokenString = JwtHelper.GenerateJwtToken(user, _configuration, out expiresInSeconds);
            return new ExternalAuthResponseDto(tokenString, "Bearer", expiresInSeconds, isNewUser);
        }
        public async Task<ExternalAuthResponseDto?> AppleAuthenticateAsync(AppleAuthDto command)
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(command.IdToken);

            using var httpClient = new HttpClient();
            var keys = await httpClient.GetFromJsonAsync<DTOs.AppleKeys>("https://appleid.apple.com/auth/keys");
            if (keys == null || keys.Keys == null || !keys.Keys.Any())
                throw ApiExceptions.InternalServerError("Unable to retrieve Apple public keys.");

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = "https://appleid.apple.com",
                ValidAudience = _configuration["Apple:ClientId"] ?? "", // Set your Apple client ID here
                IssuerSigningKeys = keys.Keys.Select(k => new RsaSecurityKey(new System.Security.Cryptography.RSAParameters
                {
                    Modulus = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(k.N),
                    Exponent = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(k.E)
                })),
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true
            };

            handler.ValidateToken(command.IdToken, validationParameters, out var validatedToken);
            var jwt2 = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;

            var email = jwt2.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var firstName = jwt2.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
            var lastName = jwt2.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;
            var sub = jwt2.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(email))
                throw ApiExceptions.ValidationError("Invalid Apple ID token: email not found.");

            // Check if user exists by email
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            bool isNewUser = false;
            if (user == null)
            {
                // Create new user
                user = User.Create(
                    userName: email,
                    email: email,
                    firstName: firstName ?? string.Empty,
                    lastName: lastName ?? string.Empty,
                    emailConfirmed: true,
                    isActive: true,
                    passwordHash: string.Empty, // No password for external
                    securityStamp: Guid.NewGuid().ToString(),
                    sub: sub
                );
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();
                isNewUser = true;
            }

            // Generate JWT
            int expiresInSeconds;
            var tokenString = JwtHelper.GenerateJwtToken(user, _configuration, out expiresInSeconds);
            return new ExternalAuthResponseDto(tokenString, "Bearer", expiresInSeconds, isNewUser);
        }
    }
} 
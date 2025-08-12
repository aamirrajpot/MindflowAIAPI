using Mindflow_Web_API.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Mindflow_Web_API.Utilities
{
    public static class JwtHelper
    {
        public static string GenerateJwtToken(User user, IConfiguration configuration, out int expiresInSeconds)
        {
            var jwtSettings = configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresInMinutes = double.Parse(jwtSettings["ExpiresInMinutes"]!);
            var expires = DateTime.UtcNow.AddMinutes(expiresInMinutes);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );
            expiresInSeconds = (int)(expiresInMinutes * 60);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
} 
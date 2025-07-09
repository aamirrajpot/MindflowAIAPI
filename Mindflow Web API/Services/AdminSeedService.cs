using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace Mindflow_Web_API.Services
{
    public interface IAdminSeedService
    {
        Task SeedAdminUserAsync();
    }

    public class AdminSeedService : IAdminSeedService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<AdminSeedService> _logger;

        public AdminSeedService(MindflowDbContext dbContext, ILogger<AdminSeedService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task SeedAdminUserAsync()
        {
            var adminEmail = "admin@mindflowai.com";
            var adminExists = await _dbContext.Users.AnyAsync(u => u.Email == adminEmail);
            
            if (!adminExists)
            {
                var adminUser = new User
                {
                    UserName = "admin",
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    PasswordHash = HashPassword("Admin@123"), // Default password
                    SecurityStamp = Guid.NewGuid().ToString(),
                    EmailConfirmed = true,
                    IsActive = true
                };

                await _dbContext.Users.AddAsync(adminUser);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Default admin user seeded successfully");
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
} 
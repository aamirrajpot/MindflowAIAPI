using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System.Security.Cryptography;
using System.Text;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Services
{
    public interface IAdminSeedService
    {
        Task SeedAdminUserAsync();
        Task SeedDefaultUsersAsync();
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
                    PasswordHash = PasswordHelper.HashPassword("Admin@123"), // Default password
                    SecurityStamp = Guid.NewGuid().ToString(),
                    EmailConfirmed = true,
                    IsActive = true,
                    Role = Role.Admin
                };

                await _dbContext.Users.AddAsync(adminUser);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Default admin user seeded successfully");
            }
        }

        public async Task SeedDefaultUsersAsync()
        {
            await SeedAdminUserAsync();

            var userEmail = "aamirrajpot6@gmail.com";
            var userExists = await _dbContext.Users.AnyAsync(u => u.Email == userEmail);

            if (!userExists)
            {
                var defaultUser = new User
                {
                    UserName = "user",
                    Email = userEmail,
                    FirstName = "Default",
                    LastName = "User",
                    PasswordHash = PasswordHelper.HashPassword("User@123"),
                    SecurityStamp = Guid.NewGuid().ToString(),
                    EmailConfirmed = true,
                    IsActive = true,
                    Role = Role.User
                };

                await _dbContext.Users.AddAsync(defaultUser);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Default regular user seeded successfully");
            }
        }
    }
} 
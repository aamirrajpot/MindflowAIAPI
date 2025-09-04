using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    public class UserDataCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserDataCleanupService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromDays(7); // Run weekly

        public UserDataCleanupService(IServiceProvider serviceProvider, ILogger<UserDataCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🗑️ User Data Cleanup Service started. Will run every 7 days.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error occurred during user data cleanup");
                }

                // Wait for 7 days before next cleanup
                await Task.Delay(_period, stoppingToken);
            }
        }

        private async Task PerformCleanupAsync()
        {
            _logger.LogInformation("🧹 Starting user data cleanup process...");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MindflowDbContext>();

            // Find users deactivated more than 7 days ago
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var usersToDelete = await dbContext.Users
                .Where(u => u.DeactivatedAtUtc.HasValue && u.DeactivatedAtUtc.Value <= cutoffDate)
                .ToListAsync();

            if (!usersToDelete.Any())
            {
                _logger.LogInformation("✅ No users found for cleanup (deactivated more than 7 days ago)");
                return;
            }

            _logger.LogInformation("🗑️ Found {Count} users to permanently delete", usersToDelete.Count);

            foreach (var user in usersToDelete)
            {
                try
                {
                    await DeleteUserDataAsync(dbContext, user.Id);
                    _logger.LogInformation("✅ Successfully deleted all data for user {UserId}", user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to delete data for user {UserId}", user.Id);
                }
            }

            _logger.LogInformation("🏁 User data cleanup process completed");
        }

        private async Task DeleteUserDataAsync(MindflowDbContext dbContext, Guid userId)
        {
            _logger.LogInformation("🗑️ Deleting all data for user {UserId}", userId);

            // Delete in order to respect foreign key constraints

            // 1. Delete BrainDumpEntries
            var brainDumpEntries = await dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId)
                .ToListAsync();
            if (brainDumpEntries.Any())
            {
                dbContext.BrainDumpEntries.RemoveRange(brainDumpEntries);
                _logger.LogInformation("🗑️ Deleted {Count} brain dump entries", brainDumpEntries.Count);
            }

            // 2. Delete TaskItems
            var taskItems = await dbContext.Tasks
                .Where(t => t.UserId == userId)
                .ToListAsync();
            if (taskItems.Any())
            {
                dbContext.Tasks.RemoveRange(taskItems);
                _logger.LogInformation("🗑️ Deleted {Count} task items", taskItems.Count);
            }

            // 3. Delete WellnessCheckIns
            var wellnessCheckIns = await dbContext.WellnessCheckIns
                .Where(w => w.UserId == userId)
                .ToListAsync();
            if (wellnessCheckIns.Any())
            {
                dbContext.WellnessCheckIns.RemoveRange(wellnessCheckIns);
                _logger.LogInformation("🗑️ Deleted {Count} wellness check-ins", wellnessCheckIns.Count);
            }

            // 4. Delete UserSubscriptions
            var userSubscriptions = await dbContext.UserSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync();
            if (userSubscriptions.Any())
            {
                dbContext.UserSubscriptions.RemoveRange(userSubscriptions);
                _logger.LogInformation("🗑️ Deleted {Count} user subscriptions", userSubscriptions.Count);
            }

            // 5. Delete PaymentHistory
            var paymentHistories = await dbContext.PaymentHistory
                .Where(p => p.UserId == userId)
                .ToListAsync();
            if (paymentHistories.Any())
            {
                dbContext.PaymentHistory.RemoveRange(paymentHistories);
                _logger.LogInformation("🗑️ Deleted {Count} payment histories", paymentHistories.Count);
            }

            // 6. Delete PaymentCards
            var paymentCards = await dbContext.PaymentCards
                .Where(p => p.UserId == userId)
                .ToListAsync();
            if (paymentCards.Any())
            {
                dbContext.PaymentCards.RemoveRange(paymentCards);
                _logger.LogInformation("🗑️ Deleted {Count} payment cards", paymentCards.Count);
            }

            // 7. Delete UserOTPs
            var userOtps = await dbContext.UserOtps
                .Where(o => o.UserId == userId)
                .ToListAsync();
            if (userOtps.Any())
            {
                dbContext.UserOtps.RemoveRange(userOtps);
                _logger.LogInformation("🗑️ Deleted {Count} user OTPs", userOtps.Count);
            }

            // 8. Finally, delete the User
            var user = await dbContext.Users.FindAsync(userId);
            if (user != null)
            {
                dbContext.Users.Remove(user);
                _logger.LogInformation("🗑️ Deleted user record");
            }

            // Save all changes
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("💾 All deletions saved to database");
        }
    }
}

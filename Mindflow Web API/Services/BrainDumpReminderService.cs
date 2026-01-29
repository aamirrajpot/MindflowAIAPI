using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Services;

namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Sends a weekly reminder to users who haven't created a BrainDumpEntry in the last 7 days.
    /// Runs once per day.
    /// </summary>
    public class BrainDumpReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BrainDumpReminderService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _reminderWindow = TimeSpan.FromDays(7);

        public BrainDumpReminderService(IServiceScopeFactory scopeFactory, ILogger<BrainDumpReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BrainDumpReminderService started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendReminders(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in BrainDumpReminderService loop.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
            _logger.LogInformation("BrainDumpReminderService stopping.");
        }

        private async Task CheckAndSendReminders(CancellationToken ct)
        {
            var cutoff = DateTime.UtcNow - _reminderWindow;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MindflowDbContext>();
            var fcm = scope.ServiceProvider.GetRequiredService<IFcmNotificationService>();

            // Find users who are active and either never created a BrainDumpEntry OR last entry older than cutoff
            var users = await db.Users
                .Where(u => u.IsActive)
                .ToListAsync(ct);

            // Pre-fetch latest WellnessCheckIn per user to check if reminders are enabled for that user
            var userIds = users.Select(u => u.Id).Distinct().ToList();
            var wellnessList = await db.WellnessCheckIns
                .Where(w => userIds.Contains(w.UserId))
                .ToListAsync(ct);
            var latestWellnessByUser = wellnessList
                .GroupBy(w => w.UserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(w => w.CheckInDate).First());

            foreach (var user in users)
            {
                try
                {
                    var lastEntry = await db.BrainDumpEntries
                        .Where(b => b.UserId == user.Id)
                        .OrderByDescending(b => b.CreatedAtUtc)
                        .Select(b => (DateTime?)b.CreatedAtUtc)
                        .FirstOrDefaultAsync(ct);

                    // If user's wellness check-in does not have reminders enabled, skip
                    if (!latestWellnessByUser.TryGetValue(user.Id, out var latestWellness) || !latestWellness.ReminderEnabled)
                    {
                        continue;
                    }

                    // If user has recent entry, skip
                    if (lastEntry.HasValue && lastEntry.Value > cutoff)
                        continue;

                    // If we already sent a reminder within the window, skip
                    if (user.LastBrainDumpReminderSentAtUtc.HasValue && user.LastBrainDumpReminderSentAtUtc.Value > cutoff)
                        continue;

                    // Send notification via FCM service
                    var title = "We miss your brain dumps";
                    var body = "It's been a week since your last brain dump — take a moment to write one.";

                    var data = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["reminderType"] = "brainDump",
                        ["userId"] = user.Id.ToString()
                    };

                    var successCount = await fcm.SendToUserAsync(user.Id, title, body, data);

                    // Update user's last reminder timestamp
                    user.LastBrainDumpReminderSentAtUtc = DateTime.UtcNow;
                    db.Users.Update(user);
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("Sent brain-dump reminder to user {UserId}. DeliveredCount={Count}", user.Id, successCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send brain-dump reminder to user {UserId}", user.Id);
                }
            }
        }
    }
}


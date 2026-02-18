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
    /// Background service that scans for tasks with reminders enabled and sends FCM reminders
    /// approximately 10 minutes before the task occurrence.
    /// </summary>
    public class TaskReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TaskReminderService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _matchWindow = TimeSpan.FromSeconds(45); // tolerance around the target time

        public TaskReminderService(IServiceScopeFactory scopeFactory, ILogger<TaskReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TaskReminderService started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendReminders(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when app is stopping or token is canceled; do not log as error
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in TaskReminderService loop.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // App is stopping
                }
            }
            _logger.LogInformation("TaskReminderService stopping.");
        }

        private async Task CheckAndSendReminders(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var reminderWindowEnd = now.AddMinutes(10); // notify for tasks occurring within the next 10 minutes

            // limit DB scan by date range (yesterday..tomorrow)
            var dateMin = reminderWindowEnd.Date.AddDays(-1);
            var dateMax = reminderWindowEnd.Date.AddDays(1);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MindflowDbContext>();
            var fcm = scope.ServiceProvider.GetRequiredService<IFcmNotificationService>();

            var candidates = await db.Tasks
                .Where(t => t.IsActive && t.Date >= dateMin && t.Date <= dateMax)
                .ToListAsync(ct);

            // Pre-fetch latest WellnessCheckIn per user to check if reminders are enabled for that user
            var userIds = candidates.Select(t => t.UserId).Distinct().ToList();
            var wellnessList = await db.WellnessCheckIns
                .Where(w => userIds.Contains(w.UserId))
                .ToListAsync(ct);
            var latestWellnessByUser = wellnessList
                .GroupBy(w => w.UserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(w => w.CheckInDate).First());

            foreach (var task in candidates)
            {
                try
                {
                    var occurrenceUtc = task.Date.Date.Add(task.Time.TimeOfDay);
                    // ensure DateTimeKind.Utc for comparisons
                    if (occurrenceUtc.Kind == DateTimeKind.Unspecified)
                        occurrenceUtc = DateTime.SpecifyKind(occurrenceUtc, DateTimeKind.Utc);

                    // Send notification only if task is expiring within the next 10 minutes
                    // Skip expired tasks (occurrenceUtc < now) and tasks more than 10 minutes away
                    // This includes tasks expiring in 8 minutes, 5 minutes, etc. if they haven't been notified yet
                    if (occurrenceUtc < now || occurrenceUtc > reminderWindowEnd)
                        continue;

                    // Only send if the user's latest wellness check-in has reminders enabled
                    if (!latestWellnessByUser.TryGetValue(task.UserId, out var latestWellness) || !latestWellness.ReminderEnabled)
                    {
                        // skip sending notifications for this user's task
                        continue;
                    }

                    // avoid duplicate sends for the same occurrence
                    // avoid duplicate sends for the same task
                    if (task.LastReminderSentAtUtc.HasValue)
                    {
                        continue;
                    }

                    var title = $"Upcoming task: {task.Title}";

                    // Convert occurrence time to user's local time using timezone from latest wellness check-in
                    var localTime = occurrenceUtc;
                    var timezoneId = latestWellness.TimezoneId;
                    if (!string.IsNullOrWhiteSpace(timezoneId))
                    {
                        try
                        {
                            TimeZoneInfo timeZone;
                            try
                            {
                                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                            }
                            catch (TimeZoneNotFoundException)
                            {
                                // Map common IANA IDs to Windows IDs
                                var windowsId = timezoneId switch
                                {
                                    "America/Chicago" => "Central Standard Time",
                                    "America/New_York" => "Eastern Standard Time",
                                    "America/Denver" => "Mountain Standard Time",
                                    "America/Los_Angeles" => "Pacific Standard Time",
                                    "America/Phoenix" => "US Mountain Standard Time",
                                    _ => timezoneId
                                };
                                timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                            }

                            localTime = TimeZoneInfo.ConvertTimeFromUtc(occurrenceUtc, timeZone);
                        }
                        catch
                        {
                            // Fallback: keep UTC if timezone conversion fails
                            localTime = occurrenceUtc;
                        }
                    }

                    var body = $"Starts at {localTime:HH:mm} (in ~10 minutes).";

                    var data = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["taskId"] = task.Id.ToString()
                    };

                    var successCount = await fcm.SendToUserAsync(task.UserId, title, body, data);

                    task.LastReminderSentAtUtc = DateTime.UtcNow;
                    db.Tasks.Update(task);
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("Sent reminder for task {TaskId}. SuccessCount={Count}", task.Id, successCount);
                }
                catch (OperationCanceledException)
                {
                    throw; // Let outer handler treat shutdown gracefully
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send reminder for task {TaskId}", task.Id);
                }
            }
        }
    }
}


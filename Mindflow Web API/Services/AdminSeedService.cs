using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System.Security.Cryptography;
using System.Text;
using Mindflow_Web_API.Utilities;
using System;

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
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null)
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
                user = defaultUser; // Set user reference for wellness check-in seeding
            }

            // Seed wellness check-in for the user if it doesn't exist
            var wellnessCheckInExists = await _dbContext.WellnessCheckIns.AnyAsync(w => w.UserId == user.Id);
            if (!wellnessCheckInExists)
            {
                var timezoneId = " Asia/Karachi"; // Default timezone
                
                // Default time slots: 12 PM - 9 PM weekdays, 10 AM - 6 PM weekends
                var weekdayStartTime = "12:00";
                var weekdayStartShift = "PM";
                var weekdayEndTime = "9:00";
                var weekdayEndShift = "PM";
                
                var weekendStartTime = "10:00";
                var weekendStartShift = "AM";
                var weekendEndTime = "6:00";
                var weekendEndShift = "PM";

                // Convert local times to UTC
                var weekdayStartTimeUtc = ConvertTimeToUtc(weekdayStartTime, weekdayStartShift, timezoneId);
                var weekdayEndTimeUtc = ConvertTimeToUtc(weekdayEndTime, weekdayEndShift, timezoneId);
                var weekendStartTimeUtc = ConvertTimeToUtc(weekendStartTime, weekendStartShift, timezoneId);
                var weekendEndTimeUtc = ConvertTimeToUtc(weekendEndTime, weekendEndShift, timezoneId);

                var wellnessCheckIn = WellnessCheckIn.Create(
                    userId: user.Id,
                    moodLevel: "Okay", // Default mood
                    checkInDate: DateTime.UtcNow,
                    reminderEnabled: false,
                    reminderTime: null,
                    ageRange: "18-24", // Default age range
                    focusAreas: new[] { "Productivity", "Mental health" }, // Default focus areas
                    weekdayStartTime: weekdayStartTime,
                    weekdayStartShift: weekdayStartShift,
                    weekdayEndTime: weekdayEndTime,
                    weekdayEndShift: weekdayEndShift,
                    weekendStartTime: weekendStartTime,
                    weekendStartShift: weekendStartShift,
                    weekendEndTime: weekendEndTime,
                    weekendEndShift: weekendEndShift,
                    weekdayStartTimeUtc: weekdayStartTimeUtc,
                    weekdayEndTimeUtc: weekdayEndTimeUtc,
                    weekendStartTimeUtc: weekendStartTimeUtc,
                    weekendEndTimeUtc: weekendEndTimeUtc,
                    timezoneId: timezoneId,
                    questions: new Dictionary<string, object>()
                );

                await _dbContext.WellnessCheckIns.AddAsync(wellnessCheckIn);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Default wellness check-in seeded successfully for user {UserEmail}", userEmail);
            }
        }

        /// <summary>
        /// Converts time string with AM/PM shift from local time to UTC DateTime using timezone ID.
        /// </summary>
        private DateTime? ConvertTimeToUtc(string? timeStr, string? shift, string? timezoneId)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                return null;

            // Parse time string
            if (!TimeSpan.TryParse(timeStr, out var time))
            {
                var parts = timeStr.Trim().Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes))
                {
                    time = new TimeSpan(hours, minutes, 0);
                }
                else
                {
                    _logger.LogWarning("Failed to parse time string: {TimeStr}", timeStr);
                    return null;
                }
            }

            // Handle AM/PM shift
            if (!string.IsNullOrWhiteSpace(shift))
            {
                var shiftUpper = shift.ToUpper().Trim();
                var isPM = shiftUpper.Contains("PM");
                var isAM = shiftUpper.Contains("AM");

                if (isPM && time.Hours >= 1 && time.Hours <= 11)
                {
                    time = time.Add(new TimeSpan(12, 0, 0));
                }
                else if (isAM && time.Hours == 12)
                {
                    time = time.Subtract(new TimeSpan(12, 0, 0));
                }
            }

            // Convert from local time to UTC if timezone ID is provided
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

                    var today = DateTime.UtcNow.Date;
                    var localDateTime = today.Add(time);
                    var localDateTimeUnspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
                    var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTimeUnspecified, timeZone);
                    return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert time using timezone {Timezone}, assuming time is already in UTC", timezoneId);
                    var today = DateTime.UtcNow.Date;
                    return DateTime.SpecifyKind(today.Add(time), DateTimeKind.Utc);
                }
            }
            else
            {
                var today = DateTime.UtcNow.Date;
                return DateTime.SpecifyKind(today.Add(time), DateTimeKind.Utc);
            }
        }
    }
} 
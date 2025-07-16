using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    public class WellnessCheckInService : IWellnessCheckInService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<WellnessCheckInService> _logger;

        public WellnessCheckInService(MindflowDbContext dbContext, ILogger<WellnessCheckInService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<WellnessCheckInDto?> GetAsync(Guid userId)
        {
            var checkIn = await _dbContext.WellnessCheckIns
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CheckInDate)
                .FirstOrDefaultAsync();

            if (checkIn == null)
                return new WellnessCheckInDto(
                    Guid.Empty,
                    Guid.Empty,
                    0,
                    string.Empty,
                    string.Empty,
                    0,
                    DateTime.MinValue,
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MinValue,
                    null,
                    null,
                    false,
                    null
                );

            return new WellnessCheckInDto(
                checkIn.Id,
                checkIn.UserId,
                checkIn.StressLevel,
                checkIn.MoodLevel,
                checkIn.EnergyLevel,
                checkIn.SpiritualWellness,
                checkIn.CheckInDate,
                checkIn.Created,
                checkIn.LastModified,
                checkIn.WeekdayFreeTime,
                checkIn.WeekendFreeTime,
                checkIn.ReminderEnabled,
                checkIn.ReminderTime
            );
        }

        public async Task<WellnessCheckInDto?> PatchAsync(Guid userId, PatchWellnessCheckInDto patchDto)
        {
            var checkIn = await _dbContext.WellnessCheckIns
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CheckInDate)
                .FirstOrDefaultAsync();

            if (checkIn == null)
            {
                // Create new check-in if none exists
                checkIn = WellnessCheckIn.Create(
                    userId,
                    patchDto.StressLevel ?? 0,
                    patchDto.MoodLevel ?? string.Empty,
                    patchDto.EnergyLevel ?? string.Empty,
                    patchDto.SpiritualWellness ?? 0,
                    DateTime.UtcNow,
                    patchDto.WeekdayFreeTime,
                    patchDto.WeekendFreeTime,
                    patchDto.ReminderEnabled ?? false,
                    patchDto.ReminderTime
                );
                checkIn.CheckInDate = DateTime.UtcNow;
                await _dbContext.WellnessCheckIns.AddAsync(checkIn);
            }
            else
            {
                if (patchDto.StressLevel.HasValue)
                    checkIn.StressLevel = patchDto.StressLevel.Value;
                if (!string.IsNullOrEmpty(patchDto.MoodLevel))
                    checkIn.MoodLevel = patchDto.MoodLevel;
                if (!string.IsNullOrEmpty(patchDto.EnergyLevel))
                    checkIn.EnergyLevel = patchDto.EnergyLevel;
                if (patchDto.SpiritualWellness.HasValue)
                    checkIn.SpiritualWellness = patchDto.SpiritualWellness.Value;
                if (patchDto.WeekdayFreeTime != null)
                    checkIn.WeekdayFreeTime = patchDto.WeekdayFreeTime;
                if (patchDto.WeekendFreeTime != null)
                    checkIn.WeekendFreeTime = patchDto.WeekendFreeTime;
                if (patchDto.ReminderEnabled.HasValue)
                    checkIn.ReminderEnabled = patchDto.ReminderEnabled.Value;
                if (patchDto.ReminderTime != null)
                    checkIn.ReminderTime = patchDto.ReminderTime;
                checkIn.UpdateLastModified();
            }
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Wellness check-in PATCH upsert for user: {userId}");

            return new WellnessCheckInDto(
                checkIn.Id,
                checkIn.UserId,
                checkIn.StressLevel,
                checkIn.MoodLevel,
                checkIn.EnergyLevel,
                checkIn.SpiritualWellness,
                checkIn.CheckInDate,
                checkIn.Created,
                checkIn.LastModified,
                checkIn.WeekdayFreeTime,
                checkIn.WeekendFreeTime,
                checkIn.ReminderEnabled,
                checkIn.ReminderTime
            );
        }
    }
} 
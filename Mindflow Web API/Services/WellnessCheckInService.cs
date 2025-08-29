using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Exceptions;

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
            if (userId == Guid.Empty)
                throw ApiExceptions.ValidationError("Invalid user ID provided.");

            // Use raw SQL for ordering by CheckInDate
            var checkIn = await _dbContext.WellnessCheckIns
                .FromSqlRaw(@"
                    SELECT * FROM WellnessCheckIns 
                    WHERE UserId = {0} 
                    ORDER BY CheckInDate DESC 
                    LIMIT 1", userId)
                .FirstOrDefaultAsync();

            if (checkIn == null)
                return new WellnessCheckInDto(
                    Guid.Empty,
                    Guid.Empty,
                    string.Empty,
                    DateTime.MinValue,
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MinValue,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                );

            return new WellnessCheckInDto(
                checkIn.Id,
                checkIn.UserId,
                checkIn.MoodLevel,
                checkIn.CheckInDate,
                checkIn.Created,
                checkIn.LastModified,
                checkIn.ReminderEnabled,
                checkIn.ReminderTime,
                checkIn.AgeRange,
                checkIn.FocusAreas,
                checkIn.StressNotes,
                checkIn.ThoughtTrackingMethod,
                checkIn.SupportAreas,
                checkIn.SelfCareFrequency,
                checkIn.ToughDayMessage,
                checkIn.CopingMechanisms,
                checkIn.JoyPeaceSources,
                checkIn.WeekdayStartTime,
                checkIn.WeekdayStartShift,
                checkIn.WeekdayEndTime,
                checkIn.WeekdayEndShift,
                checkIn.WeekendStartTime,
                checkIn.WeekendStartShift,
                checkIn.WeekendEndTime,
                checkIn.WeekendEndShift
            );
        }

        public async Task<WellnessCheckInDto?> PatchAsync(Guid userId, PatchWellnessCheckInDto patchDto)
        {
            if (userId == Guid.Empty)
                throw ApiExceptions.ValidationError("Invalid user ID provided.");

            if (patchDto == null)
                throw ApiExceptions.ValidationError("Patch data cannot be null.");

            // Use raw SQL for ordering by CheckInDate
            var checkIn = await _dbContext.WellnessCheckIns
                .FromSqlRaw(@"
                    SELECT * FROM WellnessCheckIns 
                    WHERE UserId = {0} 
                    ORDER BY CheckInDate DESC 
                    LIMIT 1", userId)
                .FirstOrDefaultAsync();

            if (checkIn == null)
            {
                // Create new check-in if none exists
                checkIn = WellnessCheckIn.Create(
                    userId,
                    patchDto.MoodLevel ?? string.Empty,
                    DateTime.UtcNow,
                    patchDto.ReminderEnabled ?? false,
                    patchDto.ReminderTime,
                    patchDto.AgeRange,
                    patchDto.FocusAreas,
                    patchDto.StressNotes,
                    patchDto.ThoughtTrackingMethod,
                    patchDto.SupportAreas,
                    patchDto.SelfCareFrequency,
                    patchDto.ToughDayMessage,
                    patchDto.CopingMechanisms,
                    patchDto.JoyPeaceSources,
                    patchDto.WeekdayStartTime,
                    patchDto.WeekdayStartShift,
                    patchDto.WeekdayEndTime,
                    patchDto.WeekdayEndShift,
                    patchDto.WeekendStartTime,
                    patchDto.WeekendStartShift,
                    patchDto.WeekendEndTime,
                    patchDto.WeekendEndShift
                );
                checkIn.CheckInDate = DateTime.UtcNow;
                await _dbContext.WellnessCheckIns.AddAsync(checkIn);
            }
            else
            {
                if (!string.IsNullOrEmpty(patchDto.MoodLevel))
                    checkIn.MoodLevel = patchDto.MoodLevel;
                if (patchDto.ReminderEnabled.HasValue)
                    checkIn.ReminderEnabled = patchDto.ReminderEnabled.Value;
                if (patchDto.ReminderTime != null)
                    checkIn.ReminderTime = patchDto.ReminderTime;
                if (patchDto.AgeRange != null)
                    checkIn.AgeRange = patchDto.AgeRange;
                if (patchDto.FocusAreas != null)
                    checkIn.FocusAreas = patchDto.FocusAreas;
                if (patchDto.StressNotes != null)
                    checkIn.StressNotes = patchDto.StressNotes;
                if (patchDto.ThoughtTrackingMethod != null)
                    checkIn.ThoughtTrackingMethod = patchDto.ThoughtTrackingMethod;
                if (patchDto.SupportAreas != null)
                    checkIn.SupportAreas = patchDto.SupportAreas;
                if (patchDto.SelfCareFrequency != null)
                    checkIn.SelfCareFrequency = patchDto.SelfCareFrequency;
                if (patchDto.ToughDayMessage != null)
                    checkIn.ToughDayMessage = patchDto.ToughDayMessage;
                if (patchDto.CopingMechanisms != null)
                    checkIn.CopingMechanisms = patchDto.CopingMechanisms;
                if (patchDto.JoyPeaceSources != null)
                    checkIn.JoyPeaceSources = patchDto.JoyPeaceSources;
                if (patchDto.WeekdayStartTime != null)
                    checkIn.WeekdayStartTime = patchDto.WeekdayStartTime;
                if (patchDto.WeekdayStartShift != null)
                    checkIn.WeekdayStartShift = patchDto.WeekdayStartShift;
                if (patchDto.WeekdayEndTime != null)
                    checkIn.WeekdayEndTime = patchDto.WeekdayEndTime;
                if (patchDto.WeekdayEndShift != null)
                    checkIn.WeekdayEndShift = patchDto.WeekdayEndShift;
                if (patchDto.WeekendStartTime != null)
                    checkIn.WeekendStartTime = patchDto.WeekendStartTime;
                if (patchDto.WeekendStartShift != null)
                    checkIn.WeekendStartShift = patchDto.WeekendStartShift;
                if (patchDto.WeekendEndTime != null)
                    checkIn.WeekendEndTime = patchDto.WeekendEndTime;
                if (patchDto.WeekendEndShift != null)
                    checkIn.WeekendEndShift = patchDto.WeekendEndShift;
                checkIn.UpdateLastModified();
            }
            
            try
            {
                // Mark user's questionnaire as filled
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null && !user.QuestionnaireFilled)
                {
                    user.QuestionnaireFilled = true;
                }
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save wellness check-in for user {UserId}", userId);
                throw ApiExceptions.InternalServerError("Failed to save wellness check-in data.");
            }

            _logger.LogInformation($"Wellness check-in PATCH upsert for user: {userId}");

            return new WellnessCheckInDto(
                checkIn.Id,
                checkIn.UserId,
                checkIn.MoodLevel,
                checkIn.CheckInDate,
                checkIn.Created,
                checkIn.LastModified,
                checkIn.ReminderEnabled,
                checkIn.ReminderTime,
                checkIn.AgeRange,
                checkIn.FocusAreas,
                checkIn.StressNotes,
                checkIn.ThoughtTrackingMethod,
                checkIn.SupportAreas,
                checkIn.SelfCareFrequency,
                checkIn.ToughDayMessage,
                checkIn.CopingMechanisms,
                checkIn.JoyPeaceSources,
                checkIn.WeekdayStartTime,
                checkIn.WeekdayStartShift,
                checkIn.WeekdayEndTime,
                checkIn.WeekdayEndShift,
                checkIn.WeekendStartTime,
                checkIn.WeekendStartShift,
                checkIn.WeekendEndTime,
                checkIn.WeekendEndShift
            );
        }
    }
} 
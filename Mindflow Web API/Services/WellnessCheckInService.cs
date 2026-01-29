using JsonRepairSharp;
using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Utilities;
using System.Text.Json;
using static Google.Apis.Requests.BatchRequest;

namespace Mindflow_Web_API.Services
{
    public class WellnessCheckInService : IWellnessCheckInService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<WellnessCheckInService> _logger;
        private readonly IRunPodService _runPodService;
        private readonly WellnessDataProcessor _wellnessDataProcessor;

        public WellnessCheckInService(
            MindflowDbContext dbContext, 
            ILogger<WellnessCheckInService> logger, 
            IRunPodService runPodService,
            WellnessDataProcessor wellnessDataProcessor)
        {
            _dbContext = dbContext;
            _logger = logger;
            _runPodService = runPodService;
            _wellnessDataProcessor = wellnessDataProcessor;
        }

        public async Task<WellnessCheckInDto?> GetAsync(Guid userId)
        {
            _logger.LogInformation("Starting GetAsync for user {UserId}", userId);
            
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID provided to GetAsync: {UserId}", userId);
                throw ApiExceptions.ValidationError("Invalid user ID provided.");
            }

            try
            {
                _logger.LogDebug("Querying database for wellness check-in data for user {UserId}", userId);
                
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
                    _logger.LogInformation("No wellness check-in found for user {UserId}, returning default DTO", userId);
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
                        null,
                        null, null,
                        new Dictionary<string, object>()
                    );
                }

                _logger.LogInformation("Found wellness check-in for user {UserId}. CheckInId: {CheckInId}, MoodLevel: {MoodLevel}, CheckInDate: {CheckInDate}", 
                    userId, checkIn.Id, checkIn.MoodLevel, checkIn.CheckInDate);

                var dto = new WellnessCheckInDto(
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
                    checkIn.WeekdayStartTime,
                    checkIn.WeekdayStartShift,
                    checkIn.WeekdayEndTime,
                    checkIn.WeekdayEndShift,
                    checkIn.WeekendStartTime,
                    checkIn.WeekendStartShift,
                    checkIn.WeekendEndTime,
                    checkIn.WeekendEndShift,
                    checkIn.WeekdayStartTimeUtc,
                    checkIn.WeekdayEndTimeUtc,
                    checkIn.WeekendStartTimeUtc,
                    checkIn.WeekendEndTimeUtc,
                    checkIn.WeekdayStartMinutesUtc,
                    checkIn.WeekdayEndMinutesUtc,
                    checkIn.WeekendStartMinutesUtc,
                    checkIn.WeekendEndMinutesUtc,
                    checkIn.TimezoneId,
                    checkIn.Questions ?? new Dictionary<string, object>()
                );

                _logger.LogDebug("Successfully created wellness DTO for user {UserId}. Time slots - Weekday: {WeekdayStart}-{WeekdayEnd}, Weekend: {WeekendStart}-{WeekendEnd}", 
                    userId, 
                    !string.IsNullOrEmpty(checkIn.WeekdayStartTime) ? $"{checkIn.WeekdayStartTime} {checkIn.WeekdayStartShift}" : "Not set",
                    !string.IsNullOrEmpty(checkIn.WeekdayEndTime) ? $"{checkIn.WeekdayEndTime} {checkIn.WeekdayEndShift}" : "Not set",
                    !string.IsNullOrEmpty(checkIn.WeekendStartTime) ? $"{checkIn.WeekendStartTime} {checkIn.WeekendStartShift}" : "Not set",
                    !string.IsNullOrEmpty(checkIn.WeekendEndTime) ? $"{checkIn.WeekendEndTime} {checkIn.WeekendEndShift}" : "Not set");

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting wellness check-in for user {UserId}", userId);
                throw;
            }
        }

        public async Task<WellnessCheckInDto?> PatchAsync(Guid userId, PatchWellnessCheckInDto patchDto)
        {
            _logger.LogInformation("Starting PatchAsync for user {UserId}", userId);
            
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID provided to PatchAsync: {UserId}", userId);
                throw ApiExceptions.ValidationError("Invalid user ID provided.");
            }

            if (patchDto == null)
            {
                _logger.LogWarning("Patch data is null for user {UserId}", userId);
                throw ApiExceptions.ValidationError("Patch data cannot be null.");
            }

            var timezoneIdInput = string.IsNullOrWhiteSpace(patchDto.TimezoneId) ? null : patchDto.TimezoneId.Trim();

            _logger.LogDebug("Patch data received for user {UserId}. MoodLevel: {MoodLevel}, ReminderEnabled: {ReminderEnabled}, TimezoneId: {TimezoneId}", 
                userId, patchDto.MoodLevel, patchDto.ReminderEnabled, timezoneIdInput ?? "null");
            _logger.LogDebug("Weekday times - Start: {WeekdayStartTime} {WeekdayStartShift}, End: {WeekdayEndTime} {WeekdayEndShift}", 
                patchDto.WeekdayStartTime, patchDto.WeekdayStartShift, patchDto.WeekdayEndTime, patchDto.WeekdayEndShift);
            _logger.LogDebug("Weekend times - Start: {WeekendStartTime} {WeekendStartShift}, End: {WeekendEndTime} {WeekendEndShift}", 
                patchDto.WeekendStartTime, patchDto.WeekendStartShift, patchDto.WeekendEndTime, patchDto.WeekendEndShift);

            try
            {
                _logger.LogDebug("Querying database for existing wellness check-in for user {UserId}", userId);
                
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
                    _logger.LogInformation("No existing wellness check-in found for user {UserId}, creating new one", userId);
                    
                    // Allow null or empty moodLevel - it's optional
                    var moodLevel = patchDto.MoodLevel ?? string.Empty;
                    
                    // Create new check-in if none exists
                    var questions = patchDto.Questions ?? new Dictionary<string, object>();
                    _logger.LogDebug("Creating new wellness check-in for user {UserId}. MoodLevel: {MoodLevel}, AgeRange: {AgeRange}, FocusAreas: {FocusAreas}, QuestionsCount: {QuestionsCount}", 
                        userId, string.IsNullOrEmpty(moodLevel) ? "null" : moodLevel, patchDto.AgeRange, patchDto.FocusAreas != null ? string.Join(", ", patchDto.FocusAreas) : "null", questions.Count);
                    
                    // Store original times as-is (user input)
                    // Convert to UTC and store in UTC fields for backend processing
                    var weekdayStartTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekdayStartTime, patchDto.WeekdayStartShift, timezoneIdInput);
                    var weekdayEndTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekdayEndTime, patchDto.WeekdayEndShift, timezoneIdInput);
                    var weekendStartTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekendStartTime, patchDto.WeekendStartShift, timezoneIdInput);
                    var weekendEndTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekendEndTime, patchDto.WeekendEndShift, timezoneIdInput);
                    
                    checkIn = WellnessCheckIn.Create(
                        userId,
                        moodLevel,
                        DateTime.UtcNow,
                        patchDto.ReminderEnabled ?? false,
                        patchDto.ReminderTime,
                        patchDto.AgeRange,
                        patchDto.FocusAreas,
                        patchDto.WeekdayStartTime,  // Store original as-is
                        patchDto.WeekdayStartShift,
                        patchDto.WeekdayEndTime,
                        patchDto.WeekdayEndShift,
                        patchDto.WeekendStartTime,
                        patchDto.WeekendStartShift,
                        patchDto.WeekendEndTime,
                        patchDto.WeekendEndShift,
                        weekdayStartTimeUtc,  // Store UTC version
                        weekdayEndTimeUtc,
                        weekendStartTimeUtc,
                        weekendEndTimeUtc,
                        null, // Will be computed by WellnessDataProcessor
                        null,
                        null,
                        null,
                        timezoneIdInput,
                        questions
                    );
                    
                    // Compute UTC minute offsets for improved scheduling
                    _wellnessDataProcessor.ComputeUtcOffsets(checkIn);
                    
                    _logger.LogDebug("Created new wellness check-in for user {UserId}. CheckInId: {CheckInId}, QuestionsCount: {QuestionsCount}", 
                        userId, checkIn.Id, checkIn.Questions?.Count ?? 0);
                    
                    await _dbContext.WellnessCheckIns.AddAsync(checkIn);
                }
                else
                {
                    _logger.LogInformation("Updating existing wellness check-in for user {UserId}. CheckInId: {CheckInId}", userId, checkIn.Id);
                    
                    var fieldsUpdated = new List<string>();
                    
                    // Update fixed fields
                    if (!string.IsNullOrEmpty(patchDto.MoodLevel))
                    {
                        checkIn.MoodLevel = patchDto.MoodLevel;
                        fieldsUpdated.Add("MoodLevel");
                    }
                    if (patchDto.ReminderEnabled.HasValue)
                    {
                        checkIn.ReminderEnabled = patchDto.ReminderEnabled.Value;
                        fieldsUpdated.Add("ReminderEnabled");
                    }
                    if (patchDto.ReminderTime != null)
                    {
                        checkIn.ReminderTime = patchDto.ReminderTime;
                        fieldsUpdated.Add("ReminderTime");
                    }
                    if (patchDto.AgeRange != null)
                    {
                        checkIn.AgeRange = patchDto.AgeRange;
                        fieldsUpdated.Add("AgeRange");
                    }
                    if (patchDto.FocusAreas != null)
                    {
                        checkIn.FocusAreas = patchDto.FocusAreas;
                        fieldsUpdated.Add("FocusAreas");
                    }
                    var effectiveTimezoneId = timezoneIdInput ?? checkIn.TimezoneId;

                    // Store original times as-is (user input)
                    // Convert to UTC and store in UTC fields for backend processing
                    if (patchDto.WeekdayStartTime != null || patchDto.WeekdayStartShift != null)
                    {
                        checkIn.WeekdayStartTime = patchDto.WeekdayStartTime;
                        checkIn.WeekdayStartShift = patchDto.WeekdayStartShift;
                        checkIn.WeekdayStartTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekdayStartTime, patchDto.WeekdayStartShift, effectiveTimezoneId);
                        fieldsUpdated.Add("WeekdayStartTime");
                    }
                    if (patchDto.WeekdayEndTime != null || patchDto.WeekdayEndShift != null)
                    {
                        checkIn.WeekdayEndTime = patchDto.WeekdayEndTime;
                        checkIn.WeekdayEndShift = patchDto.WeekdayEndShift;
                        checkIn.WeekdayEndTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekdayEndTime, patchDto.WeekdayEndShift, effectiveTimezoneId);
                        fieldsUpdated.Add("WeekdayEndTime");
                    }
                    if (patchDto.WeekendStartTime != null || patchDto.WeekendStartShift != null)
                    {
                        checkIn.WeekendStartTime = patchDto.WeekendStartTime;
                        checkIn.WeekendStartShift = patchDto.WeekendStartShift;
                        checkIn.WeekendStartTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekendStartTime, patchDto.WeekendStartShift, effectiveTimezoneId);
                        fieldsUpdated.Add("WeekendStartTime");
                    }
                    if (patchDto.WeekendEndTime != null || patchDto.WeekendEndShift != null)
                    {
                        checkIn.WeekendEndTime = patchDto.WeekendEndTime;
                        checkIn.WeekendEndShift = patchDto.WeekendEndShift;
                        checkIn.WeekendEndTimeUtc = ConvertTimeToUtc24Hour(patchDto.WeekendEndTime, patchDto.WeekendEndShift, effectiveTimezoneId);
                        fieldsUpdated.Add("WeekendEndTime");
                    }
                    
                    // Merge questions dictionary
                    if (patchDto.Questions != null && patchDto.Questions.Count > 0)
                    {
                        // Initialize Questions if null
                        if (checkIn.Questions == null)
                        {
                            checkIn.Questions = new Dictionary<string, object>();
                        }
                        
                        // Merge new questions with existing ones
                        foreach (var kvp in patchDto.Questions)
                        {
                            checkIn.Questions[kvp.Key] = kvp.Value;
                            fieldsUpdated.Add($"Question:{kvp.Key}");
                        }
                        
                        _logger.LogDebug("Merged {Count} questions for user {UserId}", patchDto.Questions.Count, userId);
                    }
                    
                    // Update timezone if changed
                    if (timezoneIdInput != null)
                    {
                        checkIn.TimezoneId = timezoneIdInput;
                    }
                    
                    // Recompute UTC minute offsets if time fields or timezone changed
                    if (patchDto.WeekdayStartTime != null || patchDto.WeekdayStartShift != null ||
                        patchDto.WeekdayEndTime != null || patchDto.WeekdayEndShift != null ||
                        patchDto.WeekendStartTime != null || patchDto.WeekendStartShift != null ||
                        patchDto.WeekendEndTime != null || patchDto.WeekendEndShift != null ||
                        timezoneIdInput != null)
                    {
                        _wellnessDataProcessor.ComputeUtcOffsets(checkIn);
                        fieldsUpdated.Add("UTCMinutes");
                    }
                    
                    checkIn.Update(
                        checkIn.MoodLevel,
                        DateTime.UtcNow,
                        checkIn.ReminderEnabled,
                        checkIn.ReminderTime,
                        checkIn.AgeRange,
                        checkIn.FocusAreas,
                        checkIn.WeekdayStartTime,
                        checkIn.WeekdayStartShift,
                        checkIn.WeekdayEndTime,
                        checkIn.WeekdayEndShift,
                        checkIn.WeekendStartTime,
                        checkIn.WeekendStartShift,
                        checkIn.WeekendEndTime,
                        checkIn.WeekendEndShift,
                        checkIn.WeekdayStartTimeUtc,
                        checkIn.WeekdayEndTimeUtc,
                        checkIn.WeekendStartTimeUtc,
                        checkIn.WeekendEndTimeUtc,
                        checkIn.WeekdayStartMinutesUtc,
                        checkIn.WeekdayEndMinutesUtc,
                        checkIn.WeekendStartMinutesUtc,
                        checkIn.WeekendEndMinutesUtc,
                        effectiveTimezoneId,
                        checkIn.Questions);
                    
                    _logger.LogDebug("Updated fields for user {UserId}: {UpdatedFields}", userId, string.Join(", ", fieldsUpdated));
                }
                
                _logger.LogDebug("Marking user questionnaire as filled for user {UserId}", userId);
                
                // Mark user's questionnaire as filled
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null && !user.QuestionnaireFilled)
                {
                    user.QuestionnaireFilled = true;
                    _logger.LogInformation("Marked questionnaire as filled for user {UserId}", userId);
                }
                else if (user == null)
                {
                    _logger.LogWarning("User not found when trying to mark questionnaire as filled for user {UserId}", userId);
                }
                
                _logger.LogDebug("Saving changes to database for user {UserId}", userId);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Successfully saved wellness check-in for user {UserId}. CheckInId: {CheckInId}", userId, checkIn.Id);

                var resultDto = new WellnessCheckInDto(
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
                    checkIn.WeekdayStartTime,
                    checkIn.WeekdayStartShift,
                    checkIn.WeekdayEndTime,
                    checkIn.WeekdayEndShift,
                    checkIn.WeekendStartTime,
                    checkIn.WeekendStartShift,
                    checkIn.WeekendEndTime,
                    checkIn.WeekendEndShift,
                    checkIn.WeekdayStartTimeUtc,
                    checkIn.WeekdayEndTimeUtc,
                    checkIn.WeekendStartTimeUtc,
                    checkIn.WeekendEndTimeUtc,
                    checkIn.WeekdayStartMinutesUtc,
                    checkIn.WeekdayEndMinutesUtc,
                    checkIn.WeekendStartMinutesUtc,
                    checkIn.WeekendEndMinutesUtc,
                    checkIn.TimezoneId,
                    checkIn.Questions ?? new Dictionary<string, object>()
                );

                _logger.LogDebug("Returning updated wellness DTO for user {UserId}. Questions: {QuestionsCount}", 
                    userId, resultDto.Questions.Count);

                return resultDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save wellness check-in for user {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw ApiExceptions.InternalServerError("Failed to save wellness check-in data.");
            }
        }

        private async Task<WellnessAnalysisDto> GenerateSimpleSummaryAsync(Guid userId, WellnessCheckInDto wellnessData)
        {
            _logger.LogInformation("Starting GenerateSimpleSummaryAsync for user {UserId}", userId);
            
            try
            {
                // Validate wellness data before processing
                if (wellnessData == null || string.IsNullOrEmpty(wellnessData.MoodLevel))
                {
                    _logger.LogWarning("Wellness data is null or incomplete for user {UserId}. MoodLevel: {MoodLevel}", userId, wellnessData?.MoodLevel ?? "null");
                    return GetDefaultAnalysis();
                }

                // Extract supportNeeds from Questions dictionary
                var supportNeeds = wellnessData.Questions.TryGetValue("supportNeeds", out var supportNeedsValue) && supportNeedsValue is string[] supportNeedsArray
                    ? supportNeedsArray
                    : null;

                _logger.LogDebug("Wellness data validation passed for user {UserId}. MoodLevel: {MoodLevel}, FocusAreas: {FocusAreas}, SupportNeeds: {SupportNeeds}", 
                    userId, wellnessData.MoodLevel, 
                    wellnessData.FocusAreas != null ? string.Join(",", wellnessData.FocusAreas) : "null",
                    supportNeeds != null ? string.Join(",", supportNeeds) : "null");

                // Convert DTO to model for the prompt
                // Convert times to UTC for backend processing
                var weekdayStartTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekdayStartTime, wellnessData.WeekdayStartShift, wellnessData.TimezoneId);
                var weekdayEndTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekdayEndTime, wellnessData.WeekdayEndShift, wellnessData.TimezoneId);
                var weekendStartTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekendStartTime, wellnessData.WeekendStartShift, wellnessData.TimezoneId);
                var weekendEndTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekendEndTime, wellnessData.WeekendEndShift, wellnessData.TimezoneId);
                
                var wellnessModel = WellnessCheckIn.Create(
                    userId,
                    wellnessData.MoodLevel,
                    wellnessData.CheckInDate,
                    wellnessData.ReminderEnabled,
                    wellnessData.ReminderTime,
                    wellnessData.AgeRange,
                    wellnessData.FocusAreas,
                    wellnessData.WeekdayStartTime,
                    wellnessData.WeekdayStartShift,
                    wellnessData.WeekdayEndTime,
                    wellnessData.WeekdayEndShift,
                    wellnessData.WeekendStartTime,
                    wellnessData.WeekendStartShift,
                    wellnessData.WeekendEndTime,
                    wellnessData.WeekendEndShift,
                    weekdayStartTimeUtc,
                    weekdayEndTimeUtc,
                    weekendStartTimeUtc,
                    weekendEndTimeUtc,
                     wellnessData.WeekdayStartMinutesUtc,
                    wellnessData.WeekdayEndMinutesUtc,
                    wellnessData.WeekendStartMinutesUtc,
                    wellnessData.WeekendEndMinutesUtc,
                    wellnessData.TimezoneId,
                    wellnessData.Questions
                );

                _logger.LogDebug("Created wellness model for user {UserId}. Time slots - Weekday: {WeekdayStart}-{WeekdayEnd}, Weekend: {WeekendStart}-{WeekendEnd}", 
                    userId, 
                    !string.IsNullOrEmpty(wellnessData.WeekdayStartTime) ? $"{wellnessData.WeekdayStartTime} {wellnessData.WeekdayStartShift}" : "Not set",
                    !string.IsNullOrEmpty(wellnessData.WeekdayEndTime) ? $"{wellnessData.WeekdayEndTime} {wellnessData.WeekdayEndShift}" : "Not set",
                    !string.IsNullOrEmpty(wellnessData.WeekendStartTime) ? $"{wellnessData.WeekendStartTime} {wellnessData.WeekendStartShift}" : "Not set",
                    !string.IsNullOrEmpty(wellnessData.WeekendEndTime) ? $"{wellnessData.WeekendEndTime} {wellnessData.WeekendEndShift}" : "Not set");

                // Build a simplified prompt for faster processing
                var prompt = BuildSimpleWellnessPrompt(wellnessModel);
                _logger.LogDebug("Built simple wellness prompt for user {UserId}. Prompt length: {PromptLength}", userId, prompt.Length);

                // Call RunPod service with reduced tokens for faster response
                _logger.LogDebug("Calling RunPod service for user {UserId} with reduced tokens (400) and temperature (0.5)", userId);
                var response = await _runPodService.SendPromptAsync(prompt, 400, 0.5); // Reduced tokens and temperature
                _logger.LogDebug("Received response from RunPod service for user {UserId}. Response length: {ResponseLength}", userId, response?.Length ?? 0);

                // Parse the AI response
                _logger.LogDebug("Parsing wellness analysis response for user {UserId}", userId);
                var analysis = ParseWellnessAnalysisResponse(response ?? string.Empty);
                _logger.LogInformation("Successfully generated simple wellness summary for user {UserId}. MoodAssessment: {MoodAssessment}, UrgencyLevel: {UrgencyLevel}", 
                    userId, analysis.MoodAssessment, analysis.UrgencyLevel);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate simple wellness summary for user {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                
                // Return fallback data if AI fails
                return GetDefaultAnalysis();
            }
        }

        private WellnessAnalysisDto GetDefaultAnalysis()
        {
            return new WellnessAnalysisDto(
                "Mood assessment is being processed",
                "Moderate stress level",
                new List<string> { "emotional support", "stress management" },
                new List<string> { "deep breathing", "meditation" },
                new List<string> { "Take breaks", "Practice self-care", "Connect with others" },
                "Track daily mood and stress levels",
                3, // Default moderate urgency
                new List<string> { "Practice mindfulness", "Get adequate sleep", "Stay connected" },
                new List<string> { "Build resilience", "Maintain work-life balance" }
            );
        }

        private async Task<WellnessAnalysisResponse> GenerateAnalysisAsync(Guid userId, WellnessCheckInDto wellnessData)
        {
            _logger.LogInformation("Starting GenerateAnalysisAsync for user {UserId}", userId);
            
            try
            {
                _logger.LogDebug("Converting wellness DTO to model for user {UserId}", userId);
                
                // Convert DTO to model for the prompt
                // Convert times to UTC for backend processing
                var weekdayStartTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekdayStartTime, wellnessData.WeekdayStartShift, wellnessData.TimezoneId);
                var weekdayEndTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekdayEndTime, wellnessData.WeekdayEndShift, wellnessData.TimezoneId);
                var weekendStartTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekendStartTime, wellnessData.WeekendStartShift, wellnessData.TimezoneId);
                var weekendEndTimeUtc = ConvertTimeToUtc24Hour(wellnessData.WeekendEndTime, wellnessData.WeekendEndShift, wellnessData.TimezoneId);
                
                var wellnessModel = WellnessCheckIn.Create(
                    userId,
                    wellnessData.MoodLevel,
                    wellnessData.CheckInDate,
                    wellnessData.ReminderEnabled,
                    wellnessData.ReminderTime,
                    wellnessData.AgeRange,
                    wellnessData.FocusAreas,
                    wellnessData.WeekdayStartTime,
                    wellnessData.WeekdayStartShift,
                    wellnessData.WeekdayEndTime,
                    wellnessData.WeekdayEndShift,
                    wellnessData.WeekendStartTime,
                    wellnessData.WeekendStartShift,
                    wellnessData.WeekendEndTime,
                    wellnessData.WeekendEndShift,
                    weekdayStartTimeUtc,
                    weekdayEndTimeUtc,
                    weekendStartTimeUtc,
                    weekendEndTimeUtc,
                    wellnessData.WeekdayStartMinutesUtc,
                    wellnessData.WeekdayEndMinutesUtc,
                    wellnessData.WeekendStartMinutesUtc,
                    wellnessData.WeekendEndMinutesUtc,
                    wellnessData.TimezoneId,
                    wellnessData.Questions
                );

                // Build the wellness analysis prompt
                _logger.LogDebug("Building wellness analysis prompt for user {UserId}", userId);
                var prompt = LlamaPromptBuilderForRunpod.BuildWellnessPromptForRunpod(wellnessModel);
                _logger.LogDebug("Built wellness analysis prompt for user {UserId}. Prompt length: {PromptLength}", userId, prompt.Length);

                // Call RunPod service to get AI analysis
                _logger.LogDebug("Calling RunPod service for user {UserId} with tokens (1000) and temperature (0.7)", userId);
                var response = await _runPodService.SendPromptAsync(prompt, 1000, 0.7);
                _logger.LogDebug("Received response from RunPod service for user {UserId}. Response length: {ResponseLength}", userId, response?.Length ?? 0);

                // Parse the AI response
                _logger.LogDebug("Parsing wellness analysis response for user {UserId}", userId);
                var analysis = ParseWellnessAnalysisResponse(response ?? string.Empty);

                var analysisResponse = new WellnessAnalysisResponse(
                    Guid.NewGuid(),
                    userId,
                    wellnessData,
                    analysis,
                    DateTime.UtcNow
                );

                _logger.LogInformation("Successfully generated wellness analysis for user {UserId}. AnalysisId: {AnalysisId}", userId, analysisResponse.Id);
                return analysisResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate wellness analysis for user {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw ApiExceptions.InternalServerError("Failed to generate wellness analysis");
            }
        }

        public async Task<WellnessSummaryDto> GetWellnessSummaryAsync(Guid userId)
        {
            _logger.LogInformation("Starting GetWellnessSummaryAsync for user {UserId}", userId);
            
            try
            {
                var wellnessData = await GetAsync(userId);
                if (wellnessData == null)
                {
                    _logger.LogWarning("Wellness check-in not found for user {UserId}", userId);
                    throw ApiExceptions.NotFound("Wellness check-in not found");
                }

                // Check if this is a default/empty wellness check-in (no real data)
                if (wellnessData.UserId == Guid.Empty || string.IsNullOrEmpty(wellnessData.MoodLevel))
                {
                    _logger.LogInformation("User {UserId} has not completed wellness check-in, returning default summary", userId);
                    
                    // Return a default summary for users who haven't completed their wellness check-in
                    return new WellnessSummaryDto(
                        "general wellness",
                        "regular",
                        0,
                        new List<string> { "Complete your wellness check-in", "Set up your preferences" },
                        new List<string> { "Complete wellness questionnaire", "Set your availability", "Choose your focus areas" },
                        1, // Low urgency
                        "Complete your wellness check-in to get personalized recommendations and support tailored to your needs.",
                        null, // Insights
                        null, // Patterns
                        null, // ProgressMetrics
                        null  // EmotionTrends
                    );
                }

                _logger.LogDebug("User {UserId} has completed wellness check-in. MoodLevel: {MoodLevel}, FocusAreas: {FocusAreas}", 
                    userId, wellnessData.MoodLevel, 
                    wellnessData.FocusAreas != null ? string.Join(",", wellnessData.FocusAreas) : "null");

                // Use simplified analysis for faster response
                var analysis = await GenerateSimpleSummaryAsync(userId, wellnessData);

                // Extract primary focus from focus areas
                var primaryFocus = wellnessData.FocusAreas?.FirstOrDefault() ?? "general wellness";
                



                // Get top support needs
                var topSupportNeeds = analysis.SupportNeeds.Take(2).ToList();
                
                // Get recommended actions
                var recommendedActions = analysis.ImmediateActions.Take(3).ToList();

                // Gather data for meaningful insights
                _logger.LogDebug("Gathering progress metrics and emotion trends for user {UserId}", userId);
                var progressMetrics = await CalculateProgressMetricsAsync(userId);
                var emotionTrends = await AnalyzeEmotionTrendsAsync(userId);
                var insights = GenerateInsights(progressMetrics, emotionTrends);
                var patterns = GeneratePatterns(emotionTrends);

                // Extract selfCareFrequency from Questions dictionary
                var selfCareFrequency = wellnessData.Questions.TryGetValue("selfCareFrequency", out var selfCareValue) && selfCareValue is string selfCareStr
                    ? selfCareStr
                    : null;

                // Extract supportNeeds from Questions dictionary
                var supportNeeds = wellnessData.Questions.TryGetValue("supportNeeds", out var supportNeedsValue) && supportNeedsValue is string[] supportNeedsArray
                    ? supportNeedsArray
                    : null;

                // Create personalized message with insights
                var personalizedMessage = BuildPersonalizedMessage(primaryFocus, selfCareFrequency, progressMetrics, emotionTrends);

                _logger.LogInformation("Successfully generated wellness summary for user {UserId}. PrimaryFocus: {PrimaryFocus}, SupportNeedsCount: {SupportNeedsCount}, ActionsCount: {ActionsCount}, InsightsCount: {InsightsCount}", 
                    userId, primaryFocus, topSupportNeeds.Count, recommendedActions.Count, insights?.Count ?? 0);

                return new WellnessSummaryDto(
                    primaryFocus,
                    selfCareFrequency ?? "regular",
                    supportNeeds?.Length ?? 0,
                    topSupportNeeds,
                    recommendedActions,
                    analysis.UrgencyLevel,
                    personalizedMessage,
                    insights,
                    patterns,
                    progressMetrics,
                    emotionTrends
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting wellness summary for user {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        private string BuildSimpleWellnessPrompt(WellnessCheckIn checkIn)
        {
            _logger.LogDebug("Building simple wellness prompt for user {UserId}", checkIn.UserId);
            
            // Extract values from Questions dictionary
            var supportNeeds = checkIn.GetQuestionValue<string[]>("supportNeeds");
            var selfCareFrequency = checkIn.GetQuestionValue<string>("selfCareFrequency");
            var biggestObstacle = checkIn.GetQuestionValue<string>("biggestObstacle");
            
            // Simplified prompt focusing only on essential data for summary
            var prompt = $@"[INST] Provide a brief wellness summary based on this check-in data:

**Key Information:**
- Mood: {checkIn.MoodLevel}
- Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}
- Support Needs: {string.Join(", ", supportNeeds ?? new string[0])}
- Self-Care Frequency: {selfCareFrequency ?? "Not specified"}
- Biggest Obstacle: {biggestObstacle ?? "None"}

**Required Response (JSON format):**
{{
  ""moodAssessment"": ""Brief mood description"",
  ""stressLevel"": ""Brief stress level"",
  ""supportNeeds"": [""Need 1"", ""Need 2""],
  ""copingStrategies"": [""Strategy 1"", ""Strategy 2""],
  ""selfCareSuggestions"": [""Suggestion 1"", ""Suggestion 2""],
  ""progressTracking"": ""Brief tracking advice"",
  ""urgencyLevel"": 3,
  ""immediateActions"": [""Action 1"", ""Action 2"", ""Action 3""],
  ""longTermGoals"": [""Goal 1"", ""Goal 2""]
}}

Keep responses concise and practical. Focus on actionable items. [/INST]";

            _logger.LogDebug("Built simple wellness prompt for user {UserId}. Prompt length: {PromptLength}", checkIn.UserId, prompt.Length);
            return prompt;
        }

        private async Task<ProgressMetricsDto?> CalculateProgressMetricsAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Calculating progress metrics for user {UserId}", userId);
                
                var now = DateTime.UtcNow;
                var thisWeekStart = now.Date.AddDays(-(int)now.DayOfWeek);
                var lastWeekStart = thisWeekStart.AddDays(-7);
                var lastWeekEnd = thisWeekStart.AddDays(-1);

                // Get brain dump entries for this week and last week
                var thisWeekEntries = await _dbContext.BrainDumpEntries
                    .Where(e => e.UserId == userId 
                        && e.CreatedAtUtc >= thisWeekStart 
                        && e.DeletedAtUtc == null)
                    .ToListAsync();

                var lastWeekEntries = await _dbContext.BrainDumpEntries
                    .Where(e => e.UserId == userId 
                        && e.CreatedAtUtc >= lastWeekStart 
                        && e.CreatedAtUtc < thisWeekStart
                        && e.DeletedAtUtc == null)
                    .ToListAsync();

                // Calculate brain dump frequency
                var thisWeekCount = thisWeekEntries.Count;
                var lastWeekCount = lastWeekEntries.Count;
                var frequencyChange = thisWeekCount - lastWeekCount;

                // Calculate average mood and stress scores
                var thisWeekMoodScores = thisWeekEntries.Where(e => e.Mood.HasValue).Select(e => (double)e.Mood!.Value).ToList();
                var thisWeekStressScores = thisWeekEntries.Where(e => e.Stress.HasValue).Select(e => (double)e.Stress!.Value).ToList();
                
                var lastWeekMoodScores = lastWeekEntries.Where(e => e.Mood.HasValue).Select(e => (double)e.Mood!.Value).ToList();
                var lastWeekStressScores = lastWeekEntries.Where(e => e.Stress.HasValue).Select(e => (double)e.Stress!.Value).ToList();

                var avgMoodThisWeek = thisWeekMoodScores.Any() ? thisWeekMoodScores.Average() : 0;
                var avgStressThisWeek = thisWeekStressScores.Any() ? thisWeekStressScores.Average() : 0;
                var avgMoodLastWeek = lastWeekMoodScores.Any() ? lastWeekMoodScores.Average() : 0;
                var avgStressLastWeek = lastWeekStressScores.Any() ? lastWeekStressScores.Average() : 0;

                var moodChange = avgMoodLastWeek > 0 ? avgMoodThisWeek - avgMoodLastWeek : 0;
                var stressChange = avgStressLastWeek > 0 ? avgStressThisWeek - avgStressLastWeek : 0;

                // Calculate task completion rate
                var totalTasks = await _dbContext.Tasks
                    .Where(t => t.UserId == userId && t.IsActive)
                    .CountAsync();

                var completedTasks = await _dbContext.Tasks
                    .Where(t => t.UserId == userId && t.IsActive && t.Status == Models.TaskStatus.Completed)
                    .CountAsync();

                var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;

                // Generate interpretation
                var interpretation = BuildProgressInterpretation(avgMoodThisWeek, moodChange, avgStressThisWeek, stressChange, completionRate, frequencyChange);

                _logger.LogDebug("Calculated progress metrics for user {UserId}. CompletionRate: {CompletionRate}%, BrainDumpFrequency: {Frequency}, MoodChange: {MoodChange}", 
                    userId, completionRate, thisWeekCount, moodChange);

                return new ProgressMetricsDto(
                    completionRate,
                    thisWeekCount,
                    frequencyChange,
                    avgMoodThisWeek,
                    moodChange,
                    avgStressThisWeek,
                    stressChange,
                    interpretation
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate progress metrics for user {UserId}", userId);
                // Return default metrics instead of null
                return new ProgressMetricsDto(
                    0.0,
                    0,
                    0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    "Start tracking your wellness to see progress metrics here!"
                );
            }
        }

        private async Task<EmotionTrendsDto?> AnalyzeEmotionTrendsAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Analyzing emotion trends for user {UserId}", userId);
                
                var now = DateTime.UtcNow;
                var thisWeekStart = now.Date.AddDays(-(int)now.DayOfWeek);

                // Get brain dump entries for this week
                var entries = await _dbContext.BrainDumpEntries
                    .Where(e => e.UserId == userId 
                        && e.CreatedAtUtc >= thisWeekStart 
                        && e.DeletedAtUtc == null)
                    .ToListAsync();

                // Common emotion keywords to track
                var emotionKeywords = new[] { 
                    "anxious", "anxiety", "worried", "worry", "stressed", "stress", "overwhelmed", "overwhelm",
                    "exhausted", "exhaustion", "tired", "fatigue", "burnout", "burned out",
                    "grateful", "gratitude", "thankful", "appreciate", "happy", "happiness", "joy", "joyful",
                    "sad", "sadness", "depressed", "depression", "down", "low",
                    "calm", "peaceful", "relaxed", "content", "satisfied",
                    "frustrated", "frustration", "angry", "anger", "irritated", "irritation"
                };

                var emotionFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                
                // Count emotion keywords in brain dump text
                foreach (var entry in entries)
                {
                    var text = $"{entry.Text} {entry.Context}".ToLower();
                    foreach (var keyword in emotionKeywords)
                    {
                        var count = CountOccurrences(text, keyword);
                        if (count > 0)
                        {
                            emotionFrequency[keyword] = emotionFrequency.GetValueOrDefault(keyword, 0) + count;
                        }
                    }
                }

                // Get top 3 emotions
                var topEmotions = emotionFrequency
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(3)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // Generate emotion insights
                var emotionInsights = new List<string>();
                foreach (var emotion in topEmotions)
                {
                    var count = emotionFrequency[emotion];
                    if (count >= 3)
                    {
                        emotionInsights.Add($"You've mentioned '{emotion}' {count} times this week");
                    }
                }

                _logger.LogDebug("Analyzed emotion trends for user {UserId}. TopEmotions: {TopEmotions}, TotalEmotions: {TotalEmotions}", 
                    userId, string.Join(", ", topEmotions), emotionFrequency.Count);

                return new EmotionTrendsDto(
                    emotionFrequency,
                    topEmotions,
                    emotionInsights
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze emotion trends for user {UserId}", userId);
                // Return default emotion trends instead of null
                return new EmotionTrendsDto(
                    new Dictionary<string, int>(),
                    new List<string>(),
                    new List<string>()
                );
            }
        }

        private int CountOccurrences(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;

            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += keyword.Length;
            }
            return count;
        }

        private List<string> GenerateInsights(ProgressMetricsDto? progressMetrics, EmotionTrendsDto? emotionTrends)
        {
            var insights = new List<string>();

            if (progressMetrics != null)
            {
                // Task completion insight
                if (progressMetrics.TaskCompletionRate >= 80)
                {
                    insights.Add($"You've completed {progressMetrics.TaskCompletionRate:F0}% of your suggested tasks - great progress!");
                }
                else if (progressMetrics.TaskCompletionRate >= 50)
                {
                    insights.Add($"You've completed {progressMetrics.TaskCompletionRate:F0}% of your suggested tasks");
                }

                // Mood trend insight
                if (progressMetrics.AverageMoodScoreChange > 1)
                {
                    var oldMood = progressMetrics.AverageMoodScore - progressMetrics.AverageMoodScoreChange;
                    insights.Add($"Your mood scores have improved from {oldMood:F1}/10 to {progressMetrics.AverageMoodScore:F1}/10");
                }
                else if (progressMetrics.AverageMoodScoreChange < -1)
                {
                    var oldMood = progressMetrics.AverageMoodScore - progressMetrics.AverageMoodScoreChange;
                    insights.Add($"Your mood scores have decreased from {oldMood:F1}/10 to {progressMetrics.AverageMoodScore:F1}/10");
                }

                // Stress trend insight
                if (progressMetrics.AverageStressScoreChange < -1)
                {
                    var oldStress = progressMetrics.AverageStressScore - progressMetrics.AverageStressScoreChange;
                    insights.Add($"Your stress mentions dropped {Math.Abs(progressMetrics.AverageStressScoreChange):F1} points this week");
                }
                else if (progressMetrics.AverageStressScoreChange > 1)
                {
                    var oldStress = progressMetrics.AverageStressScore - progressMetrics.AverageStressScoreChange;
                    insights.Add($"Your stress levels increased from {oldStress:F1}/10 to {progressMetrics.AverageStressScore:F1}/10");
                }

                // Brain dump frequency insight
                if (progressMetrics.BrainDumpFrequencyChange > 0)
                {
                    insights.Add($"You've been more consistent with brain dumps this week (+{progressMetrics.BrainDumpFrequencyChange} entries)");
                }
            }

            if (emotionTrends != null && emotionTrends.EmotionInsights.Any())
            {
                insights.AddRange(emotionTrends.EmotionInsights);
            }

            // Ensure we always have at least one insight for new users
            if (insights.Count == 0)
            {
                insights.Add("Start using MindFlow to track your wellness and see personalized insights here!");
            }

            return insights;
        }

        private List<string> GeneratePatterns(EmotionTrendsDto? emotionTrends)
        {
            var patterns = new List<string>();

            if (emotionTrends != null)
            {
                // Identify recurring patterns
                foreach (var emotion in emotionTrends.TopEmotions)
                {
                    var count = emotionTrends.EmotionFrequency.GetValueOrDefault(emotion, 0);
                    if (count >= 3)
                    {
                        patterns.Add($"You've mentioned '{emotion}' {count} times this week - this might be worth exploring");
                    }
                }
            }

            return patterns;
        }

        private string BuildPersonalizedMessage(string primaryFocus, string? selfCareFrequency, ProgressMetricsDto? progressMetrics, EmotionTrendsDto? emotionTrends)
        {
            var message = $"Based on your focus on {primaryFocus} and {selfCareFrequency ?? "regular"} self-care routine, we've tailored MindFlow AI to support your mental wellness journey.";

            if (progressMetrics != null && !string.IsNullOrWhiteSpace(progressMetrics.Interpretation))
            {
                message += $" {progressMetrics.Interpretation}";
            }

            return message;
        }

        private string BuildProgressInterpretation(double avgMood, double moodChange, double avgStress, double stressChange, double completionRate, int frequencyChange)
        {
            var interpretations = new List<string>();

            if (moodChange > 1)
            {
                interpretations.Add($"Your mood has improved significantly this week");
            }
            else if (moodChange < -1)
            {
                interpretations.Add($"Your mood has decreased this week - consider focusing on self-care");
            }

            if (stressChange < -1)
            {
                interpretations.Add($"Your stress levels have decreased this week");
            }
            else if (stressChange > 1)
            {
                interpretations.Add($"Your stress levels have increased this week");
            }

            if (completionRate >= 80)
            {
                interpretations.Add($"You're making excellent progress with task completion");
            }
            else if (completionRate < 50)
            {
                interpretations.Add($"Consider breaking tasks into smaller steps to improve completion");
            }

            if (frequencyChange > 0)
            {
                interpretations.Add($"You've been more consistent with brain dumps this week");
            }

            return interpretations.Any() ? string.Join(". ", interpretations) + "." : string.Empty;
        }

        public async Task<AnalyticsDto> GetAnalyticsAsync(Guid userId)
        {
            _logger.LogInformation("Getting analytics for user {UserId}", userId);

            try
            {
                // Get wellness data
                var wellnessData = await GetAsync(userId);
                
                // Extract primary focus and self-care frequency
                var primaryFocus = wellnessData?.FocusAreas?.FirstOrDefault() ?? "general wellness";
                var selfCareFrequency = wellnessData?.Questions?.TryGetValue("selfCareFrequency", out var selfCareValue) == true && selfCareValue is string selfCareStr
                    ? selfCareStr
                    : "regular";

                // Gather analytics data - ensure we always get non-null values
                var progressMetrics = await CalculateProgressMetricsAsync(userId) ?? new ProgressMetricsDto(
                    0.0, 0, 0, 0.0, 0.0, 0.0, 0.0, 
                    "Start tracking your wellness to see progress metrics here!"
                );
                var emotionTrends = await AnalyzeEmotionTrendsAsync(userId) ?? new EmotionTrendsDto(
                    new Dictionary<string, int>(),
                    new List<string>(),
                    new List<string>()
                );
                var insights = GenerateInsights(progressMetrics, emotionTrends) ?? new List<string> { "Start using MindFlow to see insights!" };
                var patterns = GeneratePatterns(emotionTrends) ?? new List<string>();

                // Create personalized message - ensure it's never null
                var personalizedMessage = BuildPersonalizedMessage(primaryFocus, selfCareFrequency, progressMetrics, emotionTrends);
                if (string.IsNullOrWhiteSpace(personalizedMessage))
                {
                    personalizedMessage = $"Based on your focus on {primaryFocus} and {selfCareFrequency} self-care routine, we've tailored MindFlow AI to support your mental wellness journey.";
                }

                _logger.LogInformation("Successfully retrieved analytics for user {UserId}. Insights: {InsightsCount}, Patterns: {PatternsCount}",
                    userId, insights?.Count ?? 0, patterns?.Count ?? 0);

                return new AnalyticsDto(
                    insights,
                    patterns,
                    progressMetrics,
                    emotionTrends,
                    personalizedMessage
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting analytics for user {UserId}", userId);
                throw;
            }
        }

        private WellnessAnalysisDto ParseWellnessAnalysisResponse(string response)
        {
            try
            {
                _logger.LogInformation("Starting wellness analysis response parsing. Response length: {ResponseLength}", response?.Length ?? 0);
                
                // Clean up the response - remove any markdown formatting
                var cleanResponse = response?.Trim() ?? string.Empty;
                _logger.LogDebug("Initial response cleaned. Length: {CleanLength}", cleanResponse.Length);
                
                // If it's wrapped in code blocks, remove them
                if (cleanResponse.StartsWith("```json") && cleanResponse.EndsWith("```"))
                {
                    cleanResponse = cleanResponse.Substring(7, cleanResponse.Length - 10).Trim();
                    _logger.LogDebug("Removed ```json code blocks. New length: {NewLength}", cleanResponse.Length);
                }
                else if (cleanResponse.StartsWith("```") && cleanResponse.EndsWith("```"))
                {
                    cleanResponse = cleanResponse.Substring(3, cleanResponse.Length - 6).Trim();
                    _logger.LogDebug("Removed ``` code blocks. New length: {NewLength}", cleanResponse.Length);
                }
                
                string extractedText = cleanResponse;
                _logger.LogDebug("Starting RunPod response envelope parsing");
                
                try
                {
                    // Extract text from RunPod response (handles both new and old structures)
                    extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(cleanResponse);
                    
                    if (!string.IsNullOrWhiteSpace(extractedText) && extractedText != cleanResponse)
                    {
                        _logger.LogDebug("Extracted text from RunPod response. Extracted text length: {ExtractedLength}", extractedText.Length);
                    }
                    else
                    {
                        _logger.LogWarning("RunPod response structure is invalid or empty, using raw response");
                        extractedText = cleanResponse;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RunPod envelope parsing failed, falling back to plain text. Error: {ErrorMessage}", ex.Message);
                    extractedText = cleanResponse;
                }
                
                _logger.LogDebug("Starting JSON repair process. Text length: {TextLength}", extractedText.Length);
                
                // Extract JSON from the response if it contains explanatory text
                var jsonStart = extractedText.IndexOf('{');
                var jsonEnd = extractedText.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonOnly = extractedText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    _logger.LogDebug("Extracted JSON portion. JSON length: {JsonLength}", jsonOnly.Length);
                    extractedText = jsonOnly;
                }
                else
                {
                    _logger.LogWarning("No JSON object found in response, using full text");
                }
                
                var repairedText = RepairJson(extractedText);
                _logger.LogDebug("JSON repair completed. Repaired text length: {RepairedLength}", repairedText.Length);
               
                // Try to parse as JSON
                _logger.LogDebug("Attempting to deserialize wellness analysis JSON");
                var analysisData = System.Text.Json.JsonSerializer.Deserialize<WellnessAnalysisDto>(repairedText, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (analysisData == null)
                {
                    _logger.LogError("Failed to deserialize wellness analysis - result is null");
                    throw new InvalidOperationException("Failed to deserialize wellness analysis");
                }

                _logger.LogInformation("Wellness analysis parsing completed successfully. Mood assessment: {MoodAssessment}, Urgency level: {UrgencyLevel}", 
                    analysisData.MoodAssessment, analysisData.UrgencyLevel);
                
                return analysisData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wellness analysis parsing failed completely. Using fallback response. Error: {ErrorMessage}", ex.Message);
                
                // Fallback to default analysis if parsing fails
                return new WellnessAnalysisDto(
                    "Mood assessment could not be generated at this time.",
                    "Stress level evaluation is currently unavailable.",
                    new List<string> { "General support", "Emotional guidance" },
                    new List<string> { "Deep breathing", "Mindfulness practice" },
                    new List<string> { "Take regular breaks", "Practice self-care" },
                    "Track your mood and activities daily",
                    5,
                    new List<string> { "Take a moment to breathe", "Connect with support" },
                    new List<string> { "Build consistent self-care routine", "Develop coping strategies" }
                );
            }
        }
        private static string RepairJson(string json)
        {
            try
            {

                // Deep repair with JsonRepairSharp
                var repaired = JsonRepair.RepairJson(json);
                return string.IsNullOrWhiteSpace(repaired) ? json : repaired;
            }
            catch
            {
                try
                {
                    var fallback = JsonRepair.RepairJson(json);
                    return string.IsNullOrWhiteSpace(fallback) ? json : fallback;
                }
                catch { return json; }
            }
        }

        /// <summary>
        /// Converts time string with AM/PM shift from local time to UTC DateTime using timezone ID.
        /// </summary>
        /// <param name="timeStr">Time string (e.g., "07:00", "7:00", "3:30")</param>
        /// <param name="shift">AM/PM shift (e.g., "PM", "AM")</param>
        /// <param name="timezoneId">IANA timezone ID (e.g., "America/Chicago", "America/New_York"). If null, assumes times are already in UTC.</param>
        /// <returns>UTC DateTime with today's date and the specified time</returns>
        private DateTime? ConvertTimeToUtc24Hour(string? timeStr, string? shift, string? timezoneId = null)
        {
            // If time is null or empty, return null
            if (string.IsNullOrWhiteSpace(timeStr))
            {
                return null;
            }

            _logger.LogDebug("Converting time to UTC 24-hour format: timeStr='{TimeStr}', shift='{Shift}'", timeStr, shift);

            // Parse time string - handle formats like "7:00", "07:00", "3:30", etc.
            TimeSpan time;
            try
            {
                // Try parsing as TimeSpan (handles "7:00", "07:00", "3:30", etc.)
                if (!TimeSpan.TryParse(timeStr, out time))
                {
                    // If that fails, try manual parsing
                    var parts = timeStr.Trim().Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out var parsedHours) && int.TryParse(parts[1], out var parsedMinutes))
                    {
                        time = new TimeSpan(parsedHours, parsedMinutes, 0);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse time string: {TimeStr}", timeStr);
                        return null; // Return null if parsing fails
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing time string: {TimeStr}", timeStr);
                return null; // Return null if parsing fails
            }

            _logger.LogDebug("Parsed time: {Time} (Hours: {Hours}, Minutes: {Minutes})", time, time.Hours, time.Minutes);

            // Handle AM/PM shift
            // If shift is provided, convert from 12-hour to 24-hour format
            // If shift is null/empty, assume time is already in 24-hour format
            if (!string.IsNullOrWhiteSpace(shift))
            {
                var shiftUpper = shift.ToUpper().Trim();
                var isPM = shiftUpper.Contains("PM");
                var isAM = shiftUpper.Contains("AM");
                
                _logger.LogDebug("Processing shift: shift='{Shift}', isPM={IsPM}, isAM={IsAM}, currentHours={Hours}", shift, isPM, isAM, time.Hours);
                
                if (isPM)
                {
                    // For PM times:
                    // - 12:xx PM = 12:xx (noon, no conversion needed)
                    // - 1:xx PM - 11:xx PM = 13:xx - 23:xx (add 12 hours)
                    // - Don't add if already in 24-hour format (13-23)
                    if (time.Hours >= 1 && time.Hours <= 11)
                    {
                        time = time.Add(new TimeSpan(12, 0, 0));
                        _logger.LogDebug("Converted PM time: added 12 hours, new time: {Time}", time);
                    }
                    else if (time.Hours == 12)
                    {
                        // 12 PM (noon) stays as 12:xx in 24-hour format
                        _logger.LogDebug("12 PM (noon) - no conversion needed: {Time}", time);
                    }
                    else if (time.Hours > 12)
                    {
                        _logger.LogDebug("Time already in 24-hour format (PM), no conversion needed: {Time}", time);
                    }
                }
                else if (isAM)
                {
                    // For AM times, if it's 12:xx AM, convert to 00:xx
                    if (time.Hours == 12)
                    {
                        time = time.Subtract(new TimeSpan(12, 0, 0));
                        _logger.LogDebug("Converted 12 AM time: subtracted 12 hours, new time: {Time}", time);
                    }
                    // For 1-11 AM, time is already correct (no conversion needed)
                }
            }
            else
            {
                // No shift provided - assume time is already in 24-hour format
                _logger.LogDebug("No shift provided, assuming time is already in 24-hour format: {Time}", time);
            }

            // Convert from local time to UTC if timezone ID is provided
            DateTime utcDateTime;
            if (!string.IsNullOrWhiteSpace(timezoneId))
            {
                try
                {
                    // Get the timezone info
                    TimeZoneInfo timeZone;
                    try
                    {
                        // Try IANA timezone ID first (e.g., "America/Chicago")
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        // If IANA ID not found, try Windows timezone ID (e.g., "Central Standard Time")
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

                    // Create a DateTime in the user's local timezone
                    // Use today's date as reference date for the time slot
                    var today = DateTime.UtcNow.Date;
                    var localDateTime = today.Add(time);
                    
                    // Specify that this is an unspecified kind (local time in the user's timezone)
                    var localDateTimeUnspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
                    
                    // Convert to UTC using the timezone
                    utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTimeUnspecified, timeZone);
                    utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
                    
                    _logger.LogDebug("Converted from local time to UTC: timezone={Timezone}, local={LocalTime}, utc={UtcTime}", 
                        timezoneId, localDateTimeUnspecified, utcDateTime);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert time using timezone {Timezone}, assuming time is already in UTC: {Time}", 
                        timezoneId, time);
                    // Fallback: assume time is already in UTC
                    var today = DateTime.UtcNow.Date;
                    var inputTime = today.Add(time);
                    utcDateTime = DateTime.SpecifyKind(inputTime, DateTimeKind.Utc);
                }
            }
            else
            {
                _logger.LogDebug("No timezone ID provided, assuming time is already in UTC: {Time}", time);
                // Convert to UTC DateTime
                // Use today's date as reference date for the time slot
                var today = DateTime.UtcNow.Date;
                var inputTime = today.Add(time);
                utcDateTime = DateTime.SpecifyKind(inputTime, DateTimeKind.Utc);
            }
            
            _logger.LogDebug("Final converted time: {TimeUtc} (from {TimeStr} {Shift}, timezone={Timezone})", 
                utcDateTime, timeStr, shift, timezoneId ?? "null");
            
            // Return UTC DateTime
            return utcDateTime;
        }
    }
} 
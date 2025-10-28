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

        public WellnessCheckInService(MindflowDbContext dbContext, ILogger<WellnessCheckInService> logger, IRunPodService runPodService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _runPodService = runPodService;
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
                        null
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

            _logger.LogDebug("Patch data received for user {UserId}. MoodLevel: {MoodLevel}, ReminderEnabled: {ReminderEnabled}, WeekdayStartTime: {WeekdayStartTime}, WeekendStartTime: {WeekendStartTime}", 
                userId, patchDto.MoodLevel, patchDto.ReminderEnabled, patchDto.WeekdayStartTime, patchDto.WeekendStartTime);

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
                    
                    _logger.LogDebug("Created new wellness check-in for user {UserId}. CheckInId: {CheckInId}", userId, checkIn.Id);
                    
                    await _dbContext.WellnessCheckIns.AddAsync(checkIn);
                }
                else
                {
                    _logger.LogInformation("Updating existing wellness check-in for user {UserId}. CheckInId: {CheckInId}", userId, checkIn.Id);
                    
                    var fieldsUpdated = new List<string>();
                    
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
                    if (patchDto.StressNotes != null)
                    {
                        checkIn.StressNotes = patchDto.StressNotes;
                        fieldsUpdated.Add("StressNotes");
                    }
                    if (patchDto.ThoughtTrackingMethod != null)
                    {
                        checkIn.ThoughtTrackingMethod = patchDto.ThoughtTrackingMethod;
                        fieldsUpdated.Add("ThoughtTrackingMethod");
                    }
                    if (patchDto.SupportAreas != null)
                    {
                        checkIn.SupportAreas = patchDto.SupportAreas;
                        fieldsUpdated.Add("SupportAreas");
                    }
                    if (patchDto.SelfCareFrequency != null)
                    {
                        checkIn.SelfCareFrequency = patchDto.SelfCareFrequency;
                        fieldsUpdated.Add("SelfCareFrequency");
                    }
                    if (patchDto.ToughDayMessage != null)
                    {
                        checkIn.ToughDayMessage = patchDto.ToughDayMessage;
                        fieldsUpdated.Add("ToughDayMessage");
                    }
                    if (patchDto.CopingMechanisms != null)
                    {
                        checkIn.CopingMechanisms = patchDto.CopingMechanisms;
                        fieldsUpdated.Add("CopingMechanisms");
                    }
                    if (patchDto.JoyPeaceSources != null)
                    {
                        checkIn.JoyPeaceSources = patchDto.JoyPeaceSources;
                        fieldsUpdated.Add("JoyPeaceSources");
                    }
                    if (patchDto.WeekdayStartTime != null)
                    {
                        checkIn.WeekdayStartTime = patchDto.WeekdayStartTime;
                        fieldsUpdated.Add("WeekdayStartTime");
                    }
                    if (patchDto.WeekdayStartShift != null)
                    {
                        checkIn.WeekdayStartShift = patchDto.WeekdayStartShift;
                        fieldsUpdated.Add("WeekdayStartShift");
                    }
                    if (patchDto.WeekdayEndTime != null)
                    {
                        checkIn.WeekdayEndTime = patchDto.WeekdayEndTime;
                        fieldsUpdated.Add("WeekdayEndTime");
                    }
                    if (patchDto.WeekdayEndShift != null)
                    {
                        checkIn.WeekdayEndShift = patchDto.WeekdayEndShift;
                        fieldsUpdated.Add("WeekdayEndShift");
                    }
                    if (patchDto.WeekendStartTime != null)
                    {
                        checkIn.WeekendStartTime = patchDto.WeekendStartTime;
                        fieldsUpdated.Add("WeekendStartTime");
                    }
                    if (patchDto.WeekendStartShift != null)
                    {
                        checkIn.WeekendStartShift = patchDto.WeekendStartShift;
                        fieldsUpdated.Add("WeekendStartShift");
                    }
                    if (patchDto.WeekendEndTime != null)
                    {
                        checkIn.WeekendEndTime = patchDto.WeekendEndTime;
                        fieldsUpdated.Add("WeekendEndTime");
                    }
                    if (patchDto.WeekendEndShift != null)
                    {
                        checkIn.WeekendEndShift = patchDto.WeekendEndShift;
                        fieldsUpdated.Add("WeekendEndShift");
                    }
                    
                    checkIn.UpdateLastModified();
                    
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

                _logger.LogDebug("Returning updated wellness DTO for user {UserId}. Time slots - Weekday: {WeekdayStart}-{WeekdayEnd}, Weekend: {WeekendStart}-{WeekendEnd}", 
                    userId, 
                    !string.IsNullOrEmpty(checkIn.WeekdayStartTime) ? $"{checkIn.WeekdayStartTime} {checkIn.WeekdayStartShift}" : "Not set",
                    !string.IsNullOrEmpty(checkIn.WeekdayEndTime) ? $"{checkIn.WeekdayEndTime} {checkIn.WeekdayEndShift}" : "Not set",
                    !string.IsNullOrEmpty(checkIn.WeekendStartTime) ? $"{checkIn.WeekendStartTime} {checkIn.WeekendStartShift}" : "Not set",
                    !string.IsNullOrEmpty(checkIn.WeekendEndTime) ? $"{checkIn.WeekendEndTime} {checkIn.WeekendEndShift}" : "Not set");

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

                _logger.LogDebug("Wellness data validation passed for user {UserId}. MoodLevel: {MoodLevel}, FocusAreas: {FocusAreas}, SupportAreas: {SupportAreas}", 
                    userId, wellnessData.MoodLevel, 
                    wellnessData.FocusAreas != null ? string.Join(",", wellnessData.FocusAreas) : "null",
                    wellnessData.SupportAreas != null ? string.Join(",", wellnessData.SupportAreas) : "null");

                // Convert DTO to model for the prompt
                var wellnessModel = WellnessCheckIn.Create(
                    userId,
                    wellnessData.MoodLevel,
                    wellnessData.CheckInDate,
                    wellnessData.ReminderEnabled,
                    wellnessData.ReminderTime,
                    wellnessData.AgeRange,
                    wellnessData.FocusAreas,
                    wellnessData.StressNotes,
                    wellnessData.ThoughtTrackingMethod,
                    wellnessData.SupportAreas,
                    wellnessData.SelfCareFrequency,
                    wellnessData.ToughDayMessage,
                    wellnessData.CopingMechanisms,
                    wellnessData.JoyPeaceSources,
                    wellnessData.WeekdayStartTime,
                    wellnessData.WeekdayStartShift,
                    wellnessData.WeekdayEndTime,
                    wellnessData.WeekdayEndShift,
                    wellnessData.WeekendStartTime,
                    wellnessData.WeekendStartShift,
                    wellnessData.WeekendEndTime,
                    wellnessData.WeekendEndShift
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
                var wellnessModel = WellnessCheckIn.Create(
                    userId,
                    wellnessData.MoodLevel,
                    wellnessData.CheckInDate,
                    wellnessData.ReminderEnabled,
                    wellnessData.ReminderTime,
                    wellnessData.AgeRange,
                    wellnessData.FocusAreas,
                    wellnessData.StressNotes,
                    wellnessData.ThoughtTrackingMethod,
                    wellnessData.SupportAreas,
                    wellnessData.SelfCareFrequency,
                    wellnessData.ToughDayMessage,
                    wellnessData.CopingMechanisms,
                    wellnessData.JoyPeaceSources,
                    wellnessData.WeekdayStartTime,
                    wellnessData.WeekdayStartShift,
                    wellnessData.WeekdayEndTime,
                    wellnessData.WeekdayEndShift,
                    wellnessData.WeekendStartTime,
                    wellnessData.WeekendStartShift,
                    wellnessData.WeekendEndTime,
                    wellnessData.WeekendEndShift
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
                        "Complete your wellness check-in to get personalized recommendations and support tailored to your needs."
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

                // Create personalized message
                var personalizedMessage = $"Based on your focus on {primaryFocus} and {wellnessData.SelfCareFrequency ?? "regular"} self-care routine, we've tailored MindFlow AI to support your mental wellness journey.";

                _logger.LogInformation("Successfully generated wellness summary for user {UserId}. PrimaryFocus: {PrimaryFocus}, SupportNeedsCount: {SupportNeedsCount}, ActionsCount: {ActionsCount}", 
                    userId, primaryFocus, topSupportNeeds.Count, recommendedActions.Count);

                return new WellnessSummaryDto(
                    primaryFocus,
                    wellnessData.SelfCareFrequency ?? "regular",
                    wellnessData.SupportAreas?.Length ?? 0,
                    topSupportNeeds,
                    recommendedActions,
                    analysis.UrgencyLevel,
                    personalizedMessage
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
            
            // Simplified prompt focusing only on essential data for summary
            var prompt = $@"[INST] Provide a brief wellness summary based on this check-in data:

**Key Information:**
- Mood: {checkIn.MoodLevel}
- Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}
- Support Areas: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}
- Self-Care Frequency: {checkIn.SelfCareFrequency ?? "Not specified"}
- Stress Notes: {checkIn.StressNotes ?? "None"}

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
                    var runpod = JsonSerializer.Deserialize<RunpodResponse>(cleanResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (runpod != null && runpod.Output != null && runpod.Output.Count > 0)
                    {
                        _logger.LogDebug("RunPod response parsed successfully. Output count: {OutputCount}", runpod.Output.Count);
                        
                        var tokens = runpod.Output
                            .SelectMany(o => o.Choices ?? new())
                            .SelectMany(c => c.Tokens ?? new())
                            .ToList();
                        
                        _logger.LogDebug("Extracted {TokenCount} tokens from RunPod response", tokens.Count);
                        
                        extractedText = tokens.Count > 0 ? string.Join(string.Empty, tokens) : extractedText;
                        _logger.LogDebug("Token extraction completed. Extracted text length: {ExtractedLength}", extractedText.Length);
                    }
                    else
                    {
                        _logger.LogWarning("RunPod response structure is invalid or empty");
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
    }
} 
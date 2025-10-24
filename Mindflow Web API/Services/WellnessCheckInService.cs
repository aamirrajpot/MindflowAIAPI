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

        private async Task<WellnessAnalysisDto> GenerateSimpleSummaryAsync(Guid userId, WellnessCheckInDto wellnessData)
        {
            try
            {
                // Validate wellness data before processing
                if (wellnessData == null || string.IsNullOrEmpty(wellnessData.MoodLevel))
                {
                    _logger.LogWarning("Wellness data is null or incomplete for user {UserId}", userId);
                    return GetDefaultAnalysis();
                }

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

                // Build a simplified prompt for faster processing
                var prompt = BuildSimpleWellnessPrompt(wellnessModel);

                // Call RunPod service with reduced tokens for faster response
                var response = await _runPodService.SendPromptAsync(prompt, 400, 0.5); // Reduced tokens and temperature

                // Parse the AI response
                var analysis = ParseWellnessAnalysisResponse(response);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate simple wellness summary for user {UserId}", userId);
                
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
            try
            {
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
                var prompt = LlamaPromptBuilderForRunpod.BuildWellnessPromptForRunpod(wellnessModel);

                // Call RunPod service to get AI analysis
                var response = await _runPodService.SendPromptAsync(prompt, 1000, 0.7);

                // Parse the AI response
                var analysis = ParseWellnessAnalysisResponse(response);

                return new WellnessAnalysisResponse(
                    Guid.NewGuid(),
                    userId,
                    wellnessData,
                    analysis,
                    DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate wellness analysis for user {UserId}", userId);
                throw ApiExceptions.InternalServerError("Failed to generate wellness analysis");
            }
        }

        public async Task<WellnessSummaryDto> GetWellnessSummaryAsync(Guid userId)
        {
            var wellnessData = await GetAsync(userId);
            if (wellnessData == null)
                throw ApiExceptions.NotFound("Wellness check-in not found");

            // Check if this is a default/empty wellness check-in (no real data)
            if (wellnessData.UserId == Guid.Empty || string.IsNullOrEmpty(wellnessData.MoodLevel))
            {
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

        private string BuildSimpleWellnessPrompt(WellnessCheckIn checkIn)
        {
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
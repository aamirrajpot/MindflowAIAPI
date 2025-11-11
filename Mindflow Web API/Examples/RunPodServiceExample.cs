using Mindflow_Web_API.Models;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Examples
{
    /// <summary>
    /// Example usage of the RunPod service for different scenarios
    /// </summary>
    public class RunPodServiceExample
    {
        private readonly IRunPodService _runPodService;

        public RunPodServiceExample(IRunPodService runPodService)
        {
            _runPodService = runPodService;
        }

        /// <summary>
        /// Example: Analyze a wellness check-in with custom parameters
        /// </summary>
        public async Task<RunPodResponse> AnalyzeWellnessExample()
        {
            // Create a sample wellness check-in
            var checkIn = WellnessCheckIn.Create(
                userId: Guid.NewGuid(),
                moodLevel: "Stressed",
                checkInDate: DateTime.UtcNow,
                reminderEnabled: false,
                reminderTime: null,
                ageRange: "25-34",
                weekdayStartTime: null,
                weekdayStartShift: null,
                weekdayEndTime: null,
                weekdayEndShift: null,
                weekendStartTime: null,
                weekendStartShift: null,
                weekendEndTime: null,
                weekendEndShift: null,
                questions: new Dictionary<string, object>
                {
                    ["focusAreas"] = new [] { "Work", "Stress Management", "Self-care" },
                    ["supportNeeds"] = new [] { "Emotional Support", "Time Management" },
                    ["stressNotes"] = "Feeling overwhelmed with project deadlines and team conflicts",
                    ["selfCareFrequency"] = "Rarely",
                    ["copingMechanisms"] = new [] { "Deep breathing", "Walking", "Talking to friends" },
                    ["joyPeaceSources"] = "Reading, Nature walks, Music",
                    ["toughDayMessage"] = "I'm trying my best but it never feels like enough"
                }
            );

            try
            {
                // Analyze with custom parameters
                var result = await _runPodService.AnalyzeWellnessAsync(
                    checkIn,
                    maxTokens: 1500,  // Allow longer responses
                    temperature: 0.8   // More creative responses
                );

                return result;
            }
            catch (Exception ex)
            {
                // Handle errors appropriately
                throw new InvalidOperationException("Failed to analyze wellness", ex);
            }
        }

        /// <summary>
        /// Example: Get personalized task suggestions
        /// </summary>
        public async Task<List<TaskSuggestion>> GetTaskSuggestionsExample()
        {
            var checkIn = WellnessCheckIn.Create(
                userId: Guid.NewGuid(),
                moodLevel: "Anxious",
                checkInDate: DateTime.UtcNow,
                reminderEnabled: false,
                reminderTime: null,
                ageRange: "18-24",
                weekdayStartTime: null,
                weekdayStartShift: null,
                weekdayEndTime: null,
                weekdayEndShift: null,
                weekendStartTime: null,
                weekendStartShift: null,
                weekendEndTime: null,
                weekendEndShift: null,
                questions: new Dictionary<string, object>
                {
                    ["focusAreas"] = new [] { "Academic Performance", "Social Anxiety" },
                    ["supportNeeds"] = new [] { "Study Skills", "Social Skills" },
                    ["stressNotes"] = "Upcoming exams and feeling isolated from peers",
                    ["selfCareFrequency"] = "Sometimes",
                    ["copingMechanisms"] = new [] { "Meditation", "Exercise", "Journaling" },
                    ["joyPeaceSources"] = "Art, Music, Nature"
                }
            );

            try
            {
                var tasks = await _runPodService.GetTaskSuggestionsAsync(
                    checkIn,
                    maxTokens: 1000,
                    temperature: 0.7
                );

                return tasks;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to get task suggestions", ex);
            }
        }

        /// <summary>
        /// Example: Assess urgency level
        /// </summary>
        public async Task<UrgencyAssessment> AssessUrgencyExample()
        {
            var checkIn = WellnessCheckIn.Create(
                userId: Guid.NewGuid(),
                moodLevel: "Overwhelmed",
                checkInDate: DateTime.UtcNow,
                reminderEnabled: false,
                reminderTime: null,
                ageRange: "35-44",
                weekdayStartTime: null,
                weekdayStartShift: null,
                weekdayEndTime: null,
                weekdayEndShift: null,
                weekendStartTime: null,
                weekendStartShift: null,
                weekendEndTime: null,
                weekendEndShift: null,
                questions: new Dictionary<string, object>
                {
                    ["focusAreas"] = new [] { "Work-Life Balance", "Family Stress" },
                    ["supportNeeds"] = new [] { "Professional Help", "Family Support" },
                    ["stressNotes"] = "Feeling hopeless and considering drastic changes",
                    ["copingMechanisms"] = new [] { "None currently" },
                    ["joyPeaceSources"] = "None",
                    ["toughDayMessage"] = "I don't know how much longer I can keep going"
                }
            );

            try
            {
                var assessment = await _runPodService.AssessUrgencyAsync(
                    checkIn,
                    maxTokens: 800,
                    temperature: 0.6  // More focused responses for urgency
                );

                return assessment;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to assess urgency", ex);
            }
        }

        /// <summary>
        /// Example: Send a custom prompt
        /// </summary>
        public async Task<string> SendCustomPromptExample()
        {
            var customPrompt = @"[INST] You are a wellness coach. Please provide 3 quick tips for managing stress during a busy workday. Keep each tip under 2 sentences. [/INST]";

            try
            {
                var response = await _runPodService.SendPromptAsync(
                    customPrompt,
                    maxTokens: 500,
                    temperature: 0.9  // Very creative for tips
                );

                return response;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to send custom prompt", ex);
            }
        }

        /// <summary>
        /// Example: Batch process multiple wellness check-ins
        /// </summary>
        public async Task<List<RunPodResponse>> BatchProcessExample(List<WellnessCheckIn> checkIns)
        {
            var results = new List<RunPodResponse>();

            foreach (var checkIn in checkIns)
            {
                try
                {
                    var result = await _runPodService.AnalyzeWellnessAsync(checkIn);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other check-ins
                    Console.WriteLine($"Error processing check-in {checkIn.UserId}: {ex.Message}");
                    
                    // Add a failed result
                    results.Add(new RunPodResponse 
                    { 
                        RawResponse = $"Error: {ex.Message}"
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Example: Handle different response scenarios
        /// </summary>
        public async Task<string> HandleResponseScenariosExample()
        {
            var checkIn = WellnessCheckIn.Create(
                userId: Guid.NewGuid(),
                moodLevel: "Happy",
                checkInDate: DateTime.UtcNow,
                reminderEnabled: false,
                reminderTime: null,
                ageRange: "25-34",
                weekdayStartTime: null,
                weekdayStartShift: null,
                weekdayEndTime: null,
                weekdayEndShift: null,
                weekendStartTime: null,
                weekendStartShift: null,
                weekendEndTime: null,
                weekendEndShift: null,
                questions: new Dictionary<string, object>
                {
                    ["focusAreas"] = new [] { "Personal Growth", "Relationships" },
                    ["supportNeeds"] = new [] { "Goal Setting", "Communication" },
                    ["stressNotes"] = "Feeling good but want to maintain momentum",
                    ["copingMechanisms"] = new [] { "Exercise", "Socializing", "Learning" },
                    ["joyPeaceSources"] = "Family, Friends, Hobbies"
                }
            );

            try
            {
                var result = await _runPodService.AnalyzeWellnessAsync(checkIn);

                if (result.IsSuccess)
                {
                    if (result.WellnessAnalysis != null)
                    {
                        var analysis = result.WellnessAnalysis;
                        
                        // Process structured data
                        if (analysis.UrgencyLevel >= 8)
                        {
                            return "High urgency detected - immediate attention required";
                        }
                        else if (analysis.UrgencyLevel >= 5)
                        {
                            return "Moderate urgency - monitor closely";
                        }
                        else
                        {
                            return "Low urgency - continue with current support";
                        }
                    }
                    else
                    {
                        return "Analysis completed but no structured data returned";
                    }
                }
                else
                {
                    return "Analysis failed or incomplete";
                }
            }
            catch (Exception ex)
            {
                return $"Error during analysis: {ex.Message}";
            }
        }
    }
}

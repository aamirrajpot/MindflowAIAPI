using Mindflow_Web_API.Models;
using Mindflow_Web_API.Utilities;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mindflow_Web_API.Utilities
{
    public class LlamaPromptBuilder
    {
        public static string BuildPrompt(string journalText)
        {
            return $"""
                Analyze the following journal entry and extract:

                1. Emotional Tone (e.g., Anxious, Sad, Excited, Neutral)
                2. Main Focus Areas (e.g., Work, Family, Self-confidence)
                3. Inferred Emotional Needs (e.g., Encouragement, Stress Relief, Rest)

                Return your response as JSON with keys: tone, focusAreas, and needs.

                Entry:
                "{journalText}"
                """;
        }

        public static string BuildWellnessPrompt(WellnessCheckIn checkIn)
        {
            var jsonExample = @"{
                ""moodAssessment"": ""Detailed mood analysis"",
                ""stressLevel"": ""Stress evaluation"",
                ""supportNeeds"": [""Need 1"", ""Need 2""],
                ""copingStrategies"": [""Strategy 1"", ""Strategy 2""],
                ""selfCareSuggestions"": [""Suggestion 1"", ""Suggestion 2""],
                ""progressTracking"": ""Tracking recommendations"",
                ""urgencyLevel"": 5,
                ""immediateActions"": [""Action 1"", ""Action 2""],
                ""longTermGoals"": [""Goal 1"", ""Goal 2""]
            }";

            var prompt = $"""
                Based on the following comprehensive wellness check-in data, provide personalized insights and suggestions:

                **Demographics & Context:**
                - Age Range: {checkIn.AgeRange ?? "Not specified"}
                - Current Mood: {checkIn.MoodLevel}
                - Check-in Date: {checkIn.CheckInDate:yyyy-MM-dd}

                **Focus & Goals:**
                - Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}
                - Support Areas: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}

                **Current State:**
                - Stress Notes: {checkIn.StressNotes ?? "None provided"}
                - Self-Care Frequency: {checkIn.SelfCareFrequency ?? "Not specified"}
                - Thought Tracking Method: {checkIn.ThoughtTrackingMethod ?? "Not specified"}

                **Coping & Resilience:**
                - Coping Mechanisms: {string.Join(", ", checkIn.CopingMechanisms ?? new string[0])}
                - Joy/Peace Sources: {checkIn.JoyPeaceSources ?? "None provided"}
                - Tough Day Message: {checkIn.ToughDayMessage ?? "None provided"}

                **Time & Preferences:**
                - Weekday Time: {checkIn.WeekdayStartTime ?? "Not specified"} {checkIn.WeekdayStartShift ?? ""} - {checkIn.WeekdayEndTime ?? "Not specified"} {checkIn.WeekdayEndShift ?? ""}
                - Weekend Time: {checkIn.WeekendStartTime ?? "Not specified"} {checkIn.WeekendStartShift ?? ""} - {checkIn.WeekendEndTime ?? "Not specified"} {checkIn.WeekendEndShift ?? ""}
                - Reminder Enabled: {checkIn.ReminderEnabled}
                - Reminder Time: {checkIn.ReminderTime ?? "Not set"}

                **Analysis Request:**
                Please provide a comprehensive analysis including:
                1. **Mood Assessment**: Analyze the current mood and potential triggers
                2. **Stress Level**: Evaluate stress indicators and patterns
                3. **Support Needs**: Identify what type of support would be most beneficial
                4. **Coping Strategy Recommendations**: Suggest personalized coping mechanisms
                5. **Self-Care Suggestions**: Recommend activities based on available time and preferences
                6. **Progress Tracking**: Suggest ways to track improvements
                7. **Urgency Level**: Assess if immediate support is needed (1-10 scale)

                Return your response as JSON with the following structure:
                {jsonExample}
                """;

            return prompt;
        }

        public static string BuildTaskSuggestionPrompt(WellnessCheckIn checkIn)
        {
            var prompt = $"""
                Based on this wellness check-in, suggest 3-5 personalized tasks that would be most beneficial:

                **Current State:**
                - Mood: {checkIn.MoodLevel}
                - Age: {checkIn.AgeRange ?? "Not specified"}
                - Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}
                - Support Needs: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}
                - Self-Care Frequency: {checkIn.SelfCareFrequency ?? "Not specified"}
                - Stress Notes: {checkIn.StressNotes ?? "None"}

                **Available Resources:**
                - Coping Mechanisms: {string.Join(", ", checkIn.CopingMechanisms ?? new string[0])}
                - Joy Sources: {checkIn.JoyPeaceSources ?? "None specified"}
                - Free Time: Weekdays - {checkIn.WeekdayStartTime ?? "Not specified"} {checkIn.WeekdayStartShift ?? ""} to {checkIn.WeekdayEndTime ?? "Not specified"} {checkIn.WeekdayEndShift ?? ""}, Weekends - {checkIn.WeekendStartTime ?? "Not specified"} {checkIn.WeekendStartShift ?? ""} to {checkIn.WeekendEndTime ?? "Not specified"} {checkIn.WeekendEndShift ?? ""}

                **Task Requirements:**
                1. Consider the user's current mood and stress level
                2. Align with their focus areas and support needs
                3. Use their preferred coping mechanisms
                4. Fit within their available time
                5. Build on their sources of joy and peace
                6. Be specific and actionable
                7. Include both immediate relief and long-term growth

                Return 3-5 tasks as a JSON array of strings, each task being specific and actionable.
                """;

            return prompt;
        }

        public static string BuildUrgencyAssessmentPrompt(WellnessCheckIn checkIn)
        {
            var jsonExample = @"{ ""urgencyLevel"": 5, ""reasoning"": ""Explanation"", ""immediateAction"": ""What to do now"" }";

            var prompt = $"""
                Assess the urgency level for this wellness check-in (1-10 scale, where 10 is critical):

                **Critical Indicators:**
                - Mood: {checkIn.MoodLevel}
                - Stress Notes: {checkIn.StressNotes ?? "None"}
                - Tough Day Message: {checkIn.ToughDayMessage ?? "None"}
                - Coping Mechanisms: {string.Join(", ", checkIn.CopingMechanisms ?? new string[0])}

                **Context:**
                - Age Range: {checkIn.AgeRange ?? "Not specified"}
                - Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}
                - Support Areas: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}

                **Assessment Criteria:**
                1. Mood severity (Stressed/Overwhelmed = higher urgency)
                2. Stress note content (mentions of crisis, hopelessness, etc.)
                3. Available coping mechanisms
                4. Support network availability
                5. Age-related vulnerability factors

                Return JSON: {jsonExample}
                """;

            return prompt;
        }
    }

    public class LlamaPromptBuilderForRunpod
    {
        public static object BuildRunpodRequest(string prompt, int maxTokens = 1000, double temperature = 0.7)
        {
            return new
            {
                input = new
                {
                    prompt = prompt,
                    sampling_params = new
                    {
                        max_tokens = maxTokens,
                        temperature = temperature
                    }
                }
            };
        }

        public static string BuildWellnessPromptForRunpod(WellnessCheckIn checkIn)
        {
            var jsonExample = @"{
                ""moodAssessment"": ""Detailed mood analysis"",
                ""stressLevel"": ""Stress evaluation"",
                ""supportNeeds"": [""Need 1"", ""Need 2""],
                ""copingStrategies"": [""Strategy 1"", ""Strategy 2""],
                ""selfCareSuggestions"": [""Suggestion 1"", ""Suggestion 2""],
                ""progressTracking"": ""Tracking recommendations"",
                ""urgencyLevel"": 5,
                ""immediateActions"": [""Action 1"", ""Action 2""],
                ""longTermGoals"": [""Goal 1"", ""Goal 2""]
            }";

            var prompt = "[INST] Based on the following comprehensive wellness check-in data, provide personalized insights and suggestions:\n\n" +
                "**Demographics & Context:**\n" +
                $"- Age Range: {checkIn.AgeRange ?? "Not specified"}\n" +
                $"- Current Mood: {checkIn.MoodLevel}\n" +
                $"- Check-in Date: {checkIn.CheckInDate:yyyy-MM-dd}\n\n" +
                "**Focus & Goals:**\n" +
                $"- Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}\n" +
                $"- Support Areas: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}\n\n" +
                "**Current State:**\n" +
                $"- Stress Notes: {checkIn.StressNotes ?? "None provided"}\n" +
                $"- Self-Care Frequency: {checkIn.SelfCareFrequency ?? "Not specified"}\n" +
                $"- Thought Tracking Method: {checkIn.ThoughtTrackingMethod ?? "Not specified"}\n\n" +
                "**Coping & Resilience:**\n" +
                $"- Coping Mechanisms: {string.Join(", ", checkIn.CopingMechanisms ?? new string[0])}\n" +
                $"- Joy/Peace Sources: {checkIn.JoyPeaceSources ?? "None provided"}\n" +
                $"- Tough Day Message: {checkIn.ToughDayMessage ?? "None provided"}\n\n" +
                "**Time & Preferences:**\n" +
                $"- Weekday Time: {checkIn.WeekdayStartTime ?? "Not specified"} {checkIn.WeekdayStartShift ?? ""} - {checkIn.WeekdayEndTime ?? "Not specified"} {checkIn.WeekdayEndShift ?? ""}\n" +
                $"- Weekend Time: {checkIn.WeekendStartTime ?? "Not specified"} {checkIn.WeekendStartShift ?? ""} - {checkIn.WeekendEndTime ?? "Not specified"} {checkIn.WeekendEndShift ?? ""}\n" +
                $"- Reminder Enabled: {checkIn.ReminderEnabled}\n" +
                $"- Reminder Time: {checkIn.ReminderTime ?? "Not set"}\n\n" +
                "**Analysis Request:**\n" +
                "Please provide a comprehensive analysis including:\n" +
                "1. **Mood Assessment**: Analyze the current mood and potential triggers\n" +
                "2. **Stress Level**: Evaluate stress indicators and patterns\n" +
                "3. **Support Needs**: Identify what type of support would be most beneficial\n" +
                "4. **Coping Strategy Recommendations**: Suggest personalized coping mechanisms\n" +
                "5. **Self-Care Suggestions**: Recommend activities based on available time and preferences\n" +
                "6. **Progress Tracking**: Suggest ways to track improvements\n" +
                "7. **Urgency Level**: Assess if immediate support is needed (1-10 scale)\n\n" +
                "Return your response as JSON with the following structure:\n" +
                $"{jsonExample} [/INST]";

            return prompt;
        }

        public static string BuildTaskSuggestionPromptForRunpod(WellnessCheckIn checkIn)
        {
            var prompt = "[INST] Based on this wellness check-in, suggest 3-5 personalized tasks that would be most beneficial:\n\n" +
                "**Current State:**\n" +
                $"- Mood: {checkIn.MoodLevel}\n" +
                $"- Age: {checkIn.AgeRange ?? "Not specified"}\n" +
                $"- Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}\n" +
                $"- Support Needs: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}\n" +
                $"- Self-Care Frequency: {checkIn.SelfCareFrequency ?? "Not specified"}\n" +
                $"- Stress Notes: {checkIn.StressNotes ?? "None"}\n\n" +
                "**Available Resources:**\n" +
                $"- Coping Mechanisms: {string.Join(", ", checkIn.CopingMechanisms ?? new string[0])}\n" +
                $"- Joy Sources: {checkIn.JoyPeaceSources ?? "None specified"}\n" +
                $"- Free Time: Weekdays - {checkIn.WeekdayStartTime ?? "Not specified"} {checkIn.WeekdayStartShift ?? ""} to {checkIn.WeekdayEndTime ?? "Not specified"} {checkIn.WeekdayEndShift ?? ""}, Weekends - {checkIn.WeekendStartTime ?? "Not specified"} {checkIn.WeekendStartShift ?? ""} to {checkIn.WeekendEndTime ?? "Not specified"} {checkIn.WeekendEndShift ?? ""}\n\n" +
                "**Task Requirements:**\n" +
                "1. Consider the user's current mood and stress level\n" +
                "2. Align with their focus areas and support needs\n" +
                "3. Use their preferred coping mechanisms\n" +
                "4. Fit within their available time\n" +
                "5. Build on their sources of joy and peace\n" +
                "6. Be specific and actionable\n" +
                "7. Include both immediate relief and long-term growth\n\n" +
                "Return 3-5 tasks as a JSON array of objects, each task having: task, frequency, duration, and notes fields.\n\nIMPORTANT: Format your response as a valid JSON array like this:\n[\n  {\n    \"task\": \"Task name\",\n    \"frequency\": \"Daily/Weekly/etc\",\n    \"duration\": \"Time duration\",\n    \"notes\": \"Additional details\"\n  }\n]\n\nIf you cannot format as JSON, use this numbered format:\n1. Task: \"Task name\"\n   Frequency: Daily/Weekly/etc\n   Duration: Time duration\n   Notes: Additional details [/INST]";

            return prompt;
        }

        public static string BuildUrgencyAssessmentPromptForRunpod(WellnessCheckIn checkIn)
        {
            var jsonExample = @"{ ""urgencyLevel"": 5, ""reasoning"": ""Explanation"", ""immediateAction"": ""What to do now"" }";

            var prompt = "[INST] Assess the urgency level for this wellness check-in (1-10 scale, where 10 is critical):\n\n" +
                "**Critical Indicators:**\n" +
                $"- Mood: {checkIn.MoodLevel}\n" +
                $"- Stress Notes: {checkIn.StressNotes ?? "None"}\n" +
                $"- Tough Day Message: {checkIn.ToughDayMessage ?? "None"}\n" +
                $"- Coping Mechanisms: {string.Join(", ", checkIn.CopingMechanisms ?? new string[0])}\n\n" +
                "**Context:**\n" +
                $"- Age Range: {checkIn.AgeRange ?? "Not specified"}\n" +
                $"- Focus Areas: {string.Join(", ", checkIn.FocusAreas ?? new string[0])}\n" +
                $"- Support Areas: {string.Join(", ", checkIn.SupportAreas ?? new string[0])}\n\n" +
                "**Assessment Criteria:**\n" +
                "1. Mood severity (Stressed/Overwhelmed = higher urgency)\n" +
                "2. Stress note content (mentions of crisis, hopelessness, etc.)\n" +
                "3. Available coping mechanisms\n" +
                "4. Support network availability\n" +
                "5. Age-related vulnerability factors\n\n" +
                $"Return JSON: {jsonExample} [/INST]";

            return prompt;
        }

        public static List<TaskSuggestion> ParseTaskSuggestions(string runpodResponse)
        {
            try
            {
                // Extract the tokens array from the response
                var response = System.Text.Json.JsonSerializer.Deserialize<RunpodResponse>(runpodResponse);
                if (response?.Output?.FirstOrDefault()?.Choices?.FirstOrDefault()?.Tokens == null)
                    return new List<TaskSuggestion>();

                var tokens = response.Output.First().Choices.First().Tokens;
                var fullText = string.Join("", tokens);

                // First, try to find a JSON array in the response
                var startIndex = fullText.IndexOf('[');
                var endIndex = fullText.LastIndexOf(']');
                
                if (startIndex != -1 && endIndex != -1)
                {
                    var jsonArray = fullText.Substring(startIndex, endIndex - startIndex + 1);
                    try
                    {
                        var tasks = System.Text.Json.JsonSerializer.Deserialize<List<TaskSuggestion>>(jsonArray);
                        if (tasks != null && tasks.Any())
                            return tasks;
                    }
                    catch
                    {
                        // JSON parsing failed, fall back to text parsing
                    }
                }

                // Fallback: Parse numbered text format
                return ParseNumberedTextFormat(fullText);
            }
            catch
            {
                return new List<TaskSuggestion>();
            }
        }

        private static List<TaskSuggestion> ParseNumberedTextFormat(string text)
        {
            var tasks = new List<TaskSuggestion>();
            
            // Split by numbered items (1., 2., 3., etc.)
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var currentTask = new TaskSuggestion();
            var inTask = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Check if this is a new task (starts with number and dot)
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+\."))
                {
                    // Save previous task if exists
                    if (inTask && !string.IsNullOrEmpty(currentTask.Task))
                    {
                        tasks.Add(currentTask);
                    }
                    
                    // Start new task
                    currentTask = new TaskSuggestion();
                    inTask = true;
                    
                    // Extract task name (remove number and dot)
                    var taskStart = trimmedLine.IndexOf('.') + 1;
                    if (taskStart > 0 && taskStart < trimmedLine.Length)
                    {
                        currentTask.Task = trimmedLine.Substring(taskStart).Trim();
                    }
                }
                else if (inTask)
                {
                    // Parse task details
                    if (trimmedLine.StartsWith("Frequency:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentTask.Frequency = trimmedLine.Substring("Frequency:".Length).Trim();
                    }
                    else if (trimmedLine.StartsWith("Duration:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentTask.Duration = trimmedLine.Substring("Duration:".Length).Trim();
                    }
                    else if (trimmedLine.StartsWith("Notes:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentTask.Notes = trimmedLine.Substring("Notes:".Length).Trim();
                    }
                    else if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("Task:", StringComparison.OrdinalIgnoreCase))
                    {
                        // If no specific field, append to notes
                        if (!string.IsNullOrEmpty(currentTask.Notes))
                            currentTask.Notes += " " + trimmedLine;
                        else
                            currentTask.Notes = trimmedLine;
                    }
                }
            }
            
            // Add the last task
            if (inTask && !string.IsNullOrEmpty(currentTask.Task))
            {
                tasks.Add(currentTask);
            }
            
            return tasks;
        }
    }

    public class TaskSuggestion
    {
        [JsonPropertyName("task")]
        public string Task { get; set; } = "";
        [JsonPropertyName("frequency")]
        public string Frequency { get; set; } = "";
        [JsonPropertyName("duration")]
        public string Duration { get; set; } = "";
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";
        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "Medium";
        [JsonPropertyName("suggestedTime")]
        public string SuggestedTime { get; set; } = "";
    }

    public class RunpodResponse
    {
        [JsonPropertyName("output")]
        public List<RunpodOutput> Output { get; set; } = new();
    }

    public class RunpodOutput
    {
        [JsonPropertyName("choices")]
        public List<RunpodChoice> Choices { get; set; } = new();
    }

    public class RunpodChoice
    {
        [JsonPropertyName("tokens")]
        public List<string> Tokens { get; set; } = new();
    }
}

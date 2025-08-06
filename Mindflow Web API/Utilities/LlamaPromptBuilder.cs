using Mindflow_Web_API.Models;
using Mindflow_Web_API.Utilities;

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
}

namespace Mindflow_Web_API.Utilities
{
    public static class MoodHelper
    {
        public static readonly string[] ValidMoods = 
        {
            "Shopping",
            "Okay", 
            "Stressed",
            "Overwhelmed"
        };

        public static bool IsValidMood(string? mood)
        {
            if (string.IsNullOrWhiteSpace(mood))
                return false; // Mood cannot be null/empty
                
            return ValidMoods.Contains(mood);
        }

        // For LLM training - provides structured data about moods
        public static string GetMoodDescription(string mood)
        {
            return mood switch
            {
                "Shopping" => "Feeling excited, energetic, and looking forward to activities",
                "Okay" => "Feeling neutral, balanced, and generally content",
                "Stressed" => "Feeling anxious, pressured, and experiencing stress",
                "Overwhelmed" => "Feeling completely overwhelmed, unable to cope with current situation",
                _ => "Unknown mood"
            };
        }

        public static int GetMoodIntensity(string mood)
        {
            return mood switch
            {
                "Shopping" => 1, // Most positive
                "Okay" => 2,      // Neutral
                "Stressed" => 3,  // Negative
                "Overwhelmed" => 4, // Most negative
                _ => 0
            };
        }

        public static string GetMoodCategory(string mood)
        {
            return mood switch
            {
                "Shopping" => "Positive",
                "Okay" => "Neutral", 
                "Stressed" => "Negative",
                "Overwhelmed" => "Negative",
                _ => "Unknown"
            };
        }
    }
} 
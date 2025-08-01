namespace Mindflow_Web_API.Utilities
{
    public static class SelfCareFrequencyHelper
    {
        public static readonly string[] ValidSelfCareFrequencies = 
        {
            "Rarely",
            "Sometimes",
            "Often",
            "Daily"
        };

        public static bool IsValidSelfCareFrequency(string? frequency)
        {
            if (string.IsNullOrWhiteSpace(frequency))
                return false; // Frequency cannot be null/empty
                
            return ValidSelfCareFrequencies.Contains(frequency);
        }

        // For LLM training - provides structured data about self-care frequency
        public static string GetSelfCareFrequencyDescription(string frequency)
        {
            return frequency switch
            {
                "Rarely" => "Infrequently practices intentional self-care",
                "Sometimes" => "Occasionally engages in self-care activities",
                "Often" => "Regularly practices self-care routines",
                "Daily" => "Makes self-care a daily priority",
                _ => "Unknown self-care frequency"
            };
        }

        public static int GetSelfCareFrequencyScore(string frequency)
        {
            return frequency switch
            {
                "Rarely" => 1,
                "Sometimes" => 2,
                "Often" => 3,
                "Daily" => 4,
                _ => 0
            };
        }
    }
} 
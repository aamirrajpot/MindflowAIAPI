namespace Mindflow_Web_API.Utilities
{
    public static class FocusAreasHelper
    {
        public static readonly string[] ValidFocusAreas = 
        {
            "Mental health",
            "Productivity", 
            "Relationships",
            "Career/School",
            "Finances",
            "Self-love / Confidence",
            "Physical health",
            "Spirituality"
        };

        public static readonly int MaxFocusAreas = 3;

        public static bool IsValidFocusArea(string? focusArea)
        {
            if (string.IsNullOrWhiteSpace(focusArea))
                return false; // Focus areas cannot be null/empty
                
            return ValidFocusAreas.Contains(focusArea);
        }

        public static bool IsValidFocusAreasList(string[]? focusAreas)
        {
            if (focusAreas == null || focusAreas.Length == 0)
                return true; // Empty list is valid (optional field)
                
            if (focusAreas.Length > MaxFocusAreas)
                return false; // Cannot select more than 3 areas
                
            return focusAreas.All(IsValidFocusArea);
        }

        // For LLM training - provides structured data about focus areas
        public static string GetFocusAreaDescription(string focusArea)
        {
            return focusArea switch
            {
                "Mental health" => "Psychological and emotional well-being, stress management, anxiety, depression",
                "Productivity" => "Time management, goal setting, efficiency, work optimization",
                "Relationships" => "Interpersonal connections, family, friends, romantic relationships, communication",
                "Career/School" => "Professional development, education, academic performance, career advancement",
                "Finances" => "Money management, budgeting, financial planning, debt management",
                "Self-love / Confidence" => "Self-esteem, self-acceptance, personal growth, self-worth",
                "Physical health" => "Exercise, nutrition, sleep, physical fitness, body wellness",
                "Spirituality" => "Religious beliefs, meditation, mindfulness, inner peace, purpose",
                _ => "Unknown focus area"
            };
        }

        public static string GetFocusAreasSummary(string[] focusAreas)
        {
            if (focusAreas == null || focusAreas.Length == 0)
                return "No specific focus areas selected";
                
            var descriptions = focusAreas.Select(GetFocusAreaDescription);
            return string.Join("; ", descriptions);
        }
    }
} 
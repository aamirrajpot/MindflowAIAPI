namespace Mindflow_Web_API.Utilities
{
    public static class SupportAreasHelper
    {
        public static readonly string[] ValidSupportAreas = 
        {
            "Brain dump & organize my thoughts",
            "Make sense of what I'm feeling",
            "Get help with decisions",
            "Daily mental health check-ins",
            "Reminders to care for myself",
            "Feel more in control"
        };

        public static readonly int MaxSupportAreas = 2;

        public static bool IsValidSupportArea(string? supportArea)
        {
            if (string.IsNullOrWhiteSpace(supportArea))
                return false; // Support areas cannot be null/empty
                
            return ValidSupportAreas.Contains(supportArea);
        }

        public static bool IsValidSupportAreasList(string[]? supportAreas)
        {
            if (supportAreas == null || supportAreas.Length == 0)
                return true; // Empty list is valid (optional field)
                
            if (supportAreas.Length > MaxSupportAreas)
                return false; // Cannot select more than 2 areas
                
            return supportAreas.All(IsValidSupportArea);
        }

        // For LLM training - provides structured data about support areas
        public static string GetSupportAreaDescription(string supportArea)
        {
            return supportArea switch
            {
                "Brain dump & organize my thoughts" => "Need help organizing and structuring thoughts",
                "Make sense of what I'm feeling" => "Want to understand and process emotions",
                "Get help with decisions" => "Need support in making choices and decisions",
                "Daily mental health check-ins" => "Want regular wellness monitoring",
                "Reminders to care for myself" => "Need prompts for self-care activities",
                "Feel more in control" => "Want to regain sense of control over life",
                _ => "Unknown support area"
            };
        }
    }
} 
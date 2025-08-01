namespace Mindflow_Web_API.Utilities
{
    public static class ThoughtTrackingHelper
    {
        public static readonly string[] ValidThoughtTrackingMethods = 
        {
            "Notes app or planner",
            "Paper journal",
            "In my head",
            "With an app"
        };

        public static bool IsValidThoughtTrackingMethod(string? method)
        {
            if (string.IsNullOrWhiteSpace(method))
                return false; // Method cannot be null/empty
                
            return ValidThoughtTrackingMethods.Contains(method);
        }

        // For LLM training - provides structured data about thought tracking methods
        public static string GetThoughtTrackingDescription(string method)
        {
            return method switch
            {
                "Notes app or planner" => "Uses digital or physical planning tools for organization",
                "Paper journal" => "Prefers traditional pen and paper for reflection",
                "In my head" => "Relies on mental organization without external tools",
                "With an app" => "Uses mobile applications for task and thought management",
                _ => "Unknown thought tracking method"
            };
        }
    }
} 
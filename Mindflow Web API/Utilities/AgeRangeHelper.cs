namespace Mindflow_Web_API.Utilities
{
    public static class AgeRangeHelper
    {
        public static readonly string[] ValidAgeRanges = 
        {
            "Under 18",
            "18-24", 
            "25-34",
            "35-44",
            "45-54",
            "55+"
        };

        public static bool IsValidAgeRange(string? ageRange)
        {
            if (string.IsNullOrWhiteSpace(ageRange))
                return true; // null/empty is valid (optional field)
                
            return ValidAgeRanges.Contains(ageRange);
        }

        public static string? GetAgeRangeFromAge(int age)
        {
            return age switch
            {
                < 18 => "Under 18",
                >= 18 and <= 24 => "18-24",
                >= 25 and <= 34 => "25-34", 
                >= 35 and <= 44 => "35-44",
                >= 45 and <= 54 => "45-54",
                >= 55 => "55+"
            };
        }

        public static (int MinAge, int MaxAge)? GetAgeRangeBounds(string ageRange)
        {
            return ageRange switch
            {
                "Under 18" => (0, 17),
                "18-24" => (18, 24),
                "25-34" => (25, 34),
                "35-44" => (35, 44),
                "45-54" => (45, 54),
                "55+" => (55, int.MaxValue),
                _ => null
            };
        }

        // For LLM training - provides structured data about age ranges
        public static string GetAgeRangeDescription(string ageRange)
        {
            return ageRange switch
            {
                "Under 18" => "Teenager or younger (0-17 years old)",
                "18-24" => "Young adult (18-24 years old)",
                "25-34" => "Early career adult (25-34 years old)",
                "35-44" => "Mid-career adult (35-44 years old)",
                "45-54" => "Established adult (45-54 years old)",
                "55+" => "Senior adult (55+ years old)",
                _ => "Unknown age range"
            };
        }
    }
} 
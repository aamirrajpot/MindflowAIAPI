namespace Mindflow_Web_API.Utilities
{
    public static class CopingMechanismsHelper
    {
        public static readonly string[] ValidCopingMechanisms =
        {
            "Journaling",
            "Music",
            "Exercise / Walking",
            "Deep breathing",
            "Talking to someone",
            "Crying it out",
            "Not sure yet"
        };

        public static readonly int MaxCopingMechanisms = 5; // Allow multiple selections

        public static bool IsValidCopingMechanism(string? copingMechanism)
        {
            if (string.IsNullOrWhiteSpace(copingMechanism))
                return false;

            return ValidCopingMechanisms.Contains(copingMechanism);
        }

        public static bool IsValidCopingMechanismsList(string[]? copingMechanisms)
        {
            if (copingMechanisms == null || copingMechanisms.Length == 0)
                return true; // Optional field

            if (copingMechanisms.Length > MaxCopingMechanisms)
                return false;

            return copingMechanisms.All(IsValidCopingMechanism);
        }

        public static string GetCopingMechanismDescription(string copingMechanism)
        {
            return copingMechanism switch
            {
                "Journaling" => "Writing down thoughts and feelings to process emotions",
                "Music" => "Listening to music to regulate mood and emotions",
                "Exercise / Walking" => "Physical activity to release stress and improve mood",
                "Deep breathing" => "Breathing exercises to calm the nervous system",
                "Talking to someone" => "Sharing feelings with trusted friends or family",
                "Crying it out" => "Allowing emotional release through crying",
                "Not sure yet" => "Still exploring what works best for coping",
                _ => "Unknown coping mechanism"
            };
        }

        public static string GetCopingMechanismsSummary(string[] copingMechanisms)
        {
            if (copingMechanisms == null || copingMechanisms.Length == 0)
                return "No coping mechanisms selected";

            var descriptions = copingMechanisms.Select(GetCopingMechanismDescription);
            return string.Join(", ", descriptions);
        }

        public static string GetCopingMechanismsForLLM(string[]? copingMechanisms)
        {
            if (copingMechanisms == null || copingMechanisms.Length == 0)
                return "User has not identified specific coping mechanisms yet.";

            var summary = GetCopingMechanismsSummary(copingMechanisms);
            return $"User's preferred coping mechanisms when overwhelmed: {summary}. " +
                   $"This information can be used to suggest personalized self-care strategies " +
                   $"and activities that align with their known effective coping methods.";
        }
    }
} 
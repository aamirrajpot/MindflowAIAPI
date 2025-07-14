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
    }
}

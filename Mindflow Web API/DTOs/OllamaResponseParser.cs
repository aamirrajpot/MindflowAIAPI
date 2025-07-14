using System.Text.Json.Serialization;

namespace Mindflow_Web_API.DTOs
{
    public class AiAnalysisResult
    {
        public string Tone { get; set; }
        public List<string> FocusAreas { get; set; }
        public List<string> Needs { get; set; }
    }
    public class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; }

        // Optional: include others if needed
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }
        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
    public static class OllamaResponseParser
    {
        public static AiAnalysisResult Parse(string text)
        {
            string tone = ExtractBetween(text, "Tone:", "Focus Areas:").Trim();
            string focus = ExtractBetween(text, "Focus Areas:", "Needs:").Trim();
            string needs = ExtractAfter(text, "Needs:").Trim();

            return new AiAnalysisResult
            {
                Tone = tone,
                FocusAreas = SplitAndClean(focus),
                Needs = SplitAndClean(needs)
            };
        }

        private static string ExtractBetween(string text, string start, string end)
        {
            var startIndex = text.IndexOf(start);
            if (startIndex == -1) return "";
            startIndex += start.Length;

            var endIndex = text.IndexOf(end, startIndex);
            if (endIndex == -1) return text[startIndex..];
            return text[startIndex..endIndex];
        }

        private static string ExtractAfter(string text, string keyword)
        {
            var index = text.IndexOf(keyword);
            if (index == -1) return "";
            return text[(index + keyword.Length)..];
        }

        private static List<string> SplitAndClean(string value)
        {
            return value.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();
        }

    }
}

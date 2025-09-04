using Mindflow_Web_API.Models;
using Mindflow_Web_API.Utilities;
using Mindflow_Web_API.Services;
using System.Text.Json;

namespace Mindflow_Web_API.Services
{
    public static class TaskSuggestionEngine
    {
        public static async Task<List<string>> SuggestTasksAsync(WellnessCheckIn checkIn, HttpClient client)
        {
            var prompt = LlamaPromptBuilder.BuildTaskSuggestionPrompt(checkIn);

            var requestBody = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return ExtractTasksFromLlmResponse(content?.Response);
        }

        public static async Task<WellnessAnalysisResult> AnalyzeWellnessAsync(WellnessCheckIn checkIn, HttpClient client)
        {
            var prompt = LlamaPromptBuilder.BuildWellnessPrompt(checkIn);

            var requestBody = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return ParseWellnessAnalysis(content?.Response);
        }

        public static async Task<UrgencyAssessment> AssessUrgencyAsync(WellnessCheckIn checkIn, HttpClient client)
        {
            var prompt = LlamaPromptBuilder.BuildUrgencyAssessmentPrompt(checkIn);

            var requestBody = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return ParseUrgencyAssessment(content?.Response);
        }

        public static Task SuggestUrgentSupportAsync(Guid userId)
        {
            Console.WriteLine($"[ALERT] Urgent support required for user: {userId}");
            return Task.CompletedTask;
        }

        private static List<string> ExtractTasksFromLlmResponse(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(response) ?? new List<string>();
            }
            catch
            {
                return response
                    .Split('\n')
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(3)
                    .ToList();
            }
        }

        private static WellnessAnalysisResult ParseWellnessAnalysis(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new WellnessAnalysisResult();

            try
            {
                return JsonSerializer.Deserialize<WellnessAnalysisResult>(response) ?? new WellnessAnalysisResult();
            }
            catch
            {
                return new WellnessAnalysisResult
                {
                    MoodAssessment = "Unable to parse analysis",
                    UrgencyLevel = 5
                };
            }
        }

        private static UrgencyAssessment ParseUrgencyAssessment(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new UrgencyAssessment();

            try
            {
                return JsonSerializer.Deserialize<UrgencyAssessment>(response) ?? new UrgencyAssessment();
            }
            catch
            {
                return new UrgencyAssessment
                {
                    UrgencyLevel = 5,
                    Reasoning = "Unable to parse urgency assessment",
                    ImmediateAction = "Please review manually"
                };
            }
        }

        private class OllamaResponse
        {
            public string Response { get; set; } = string.Empty;
        }
    }

    public class WellnessAnalysisResult
    {
        public string MoodAssessment { get; set; } = string.Empty;
        public string StressLevel { get; set; } = string.Empty;
        public List<string> SupportNeeds { get; set; } = new();
        public List<string> CopingStrategies { get; set; } = new();
        public List<string> SelfCareSuggestions { get; set; } = new();
        public string ProgressTracking { get; set; } = string.Empty;
        public int UrgencyLevel { get; set; } = 5;
        public List<string> ImmediateActions { get; set; } = new();
        public List<string> LongTermGoals { get; set; } = new();
    }


}

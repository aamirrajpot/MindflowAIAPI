using Mindflow_Web_API.Models;
using System.Text.Json;

namespace Mindflow_Web_API.Services
{
    public static class TaskSuggestionEngine
    {
        public static async Task<List<string>> SuggestTasksAsync(WellnessCheckIn checkIn, HttpClient client)
        {
            var prompt = $"""
            Based on the following wellness check-in values:
            - Stress Level: {checkIn.StressLevel} (1–10)
            - Mood Level: {checkIn.MoodLevel} (1:Sad, 2:Neutral, 3:Happy)
            - Energy Level: {checkIn.EnergyLevel} (1:Low, 2:Medium, 3:High)
            - Spiritual Wellness: {checkIn.SpiritualWellness} (1–10)

            Suggest 3 personalized self-care or improvement tasks. Return them in a JSON array of strings.
            """;

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

        public static Task SuggestUrgentSupportAsync(Guid userId)
        {
            Console.WriteLine($"[ALERT] Urgent support required for user: {userId}");
            return Task.CompletedTask;
        }

        private static List<string> ExtractTasksFromLlmResponse(string response)
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

        private class OllamaResponse
        {
            public string Response { get; set; } = string.Empty;
        }
    }
}

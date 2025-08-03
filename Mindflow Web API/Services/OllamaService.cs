using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Utilities;
using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mindflow_Web_API.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OllamaService> _logger;

        public OllamaService(IHttpClientFactory httpClientFactory, ILogger<OllamaService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AiAnalysisResult?> Analyze(string journalText)
        {
            var client = _httpClientFactory.CreateClient("Ollama");
            var prompt = LlamaPromptBuilder.BuildPrompt(journalText);

            var request = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama API failed: {StatusCode}", response.StatusCode);
                throw ApiExceptions.InternalServerError($"Ollama API failed with status code: {response.StatusCode}");
            } 

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            if (result?.Response == null)
                throw ApiExceptions.InternalServerError("Invalid response from Ollama API");

            return OllamaResponseParser.Parse(result.Response);
        }

        public async Task<WellnessAnalysisResult> AnalyzeWellnessAsync(WellnessCheckIn checkIn)
        {
            var client = _httpClientFactory.CreateClient("Ollama");
            var prompt = LlamaPromptBuilder.BuildWellnessPrompt(checkIn);

            var request = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama wellness analysis failed: {StatusCode}", response.StatusCode);
                throw ApiExceptions.InternalServerError($"Wellness analysis failed with status code: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            if (result?.Response == null)
                throw ApiExceptions.InternalServerError("Invalid response from Ollama API for wellness analysis");

            return ParseWellnessAnalysis(result.Response);
        }

        public async Task<List<string>> SuggestTasksAsync(WellnessCheckIn checkIn)
        {
            var client = _httpClientFactory.CreateClient("Ollama");
            var prompt = LlamaPromptBuilder.BuildTaskSuggestionPrompt(checkIn);

            var request = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama task suggestion failed: {StatusCode}", response.StatusCode);
                throw ApiExceptions.InternalServerError($"Task suggestion failed with status code: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            if (result?.Response == null)
                throw ApiExceptions.InternalServerError("Invalid response from Ollama API for task suggestions");

            return ExtractTasksFromLlmResponse(result.Response);
        }

        public async Task<UrgencyAssessment> AssessUrgencyAsync(WellnessCheckIn checkIn)
        {
            var client = _httpClientFactory.CreateClient("Ollama");
            var prompt = LlamaPromptBuilder.BuildUrgencyAssessmentPrompt(checkIn);

            var request = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/generate", request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama urgency assessment failed: {StatusCode}", response.StatusCode);
                throw ApiExceptions.InternalServerError($"Urgency assessment failed with status code: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            if (result?.Response == null)
                throw ApiExceptions.InternalServerError("Invalid response from Ollama API for urgency assessment");

            return ParseUrgencyAssessment(result.Response);
        }

        public async Task<ComprehensiveWellnessAnalysis> AnalyzeComprehensiveAsync(WellnessCheckIn checkIn)
        {
            try
            {
                // Run all analyses in parallel for efficiency
                var wellnessTask = AnalyzeWellnessAsync(checkIn);
                var tasksTask = SuggestTasksAsync(checkIn);
                var urgencyTask = AssessUrgencyAsync(checkIn);

                await Task.WhenAll(wellnessTask, tasksTask, urgencyTask);

                return new ComprehensiveWellnessAnalysis
                {
                    WellnessAnalysis = await wellnessTask,
                    SuggestedTasks = await tasksTask,
                    UrgencyAssessment = await urgencyTask,
                    AnalysisTimestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Comprehensive wellness analysis failed");
                throw ApiExceptions.InternalServerError("Comprehensive wellness analysis failed");
            }
        }

        private WellnessAnalysisResult ParseWellnessAnalysis(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new WellnessAnalysisResult();

            try
            {
                return JsonSerializer.Deserialize<WellnessAnalysisResult>(response) ?? new WellnessAnalysisResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse wellness analysis response");
                return new WellnessAnalysisResult
                {
                    MoodAssessment = "Unable to parse analysis",
                    UrgencyLevel = 5
                };
            }
        }

        private List<string> ExtractTasksFromLlmResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(response) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse task suggestions response");
                return response
                    .Split('\n')
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(3)
                    .ToList();
            }
        }

        private UrgencyAssessment ParseUrgencyAssessment(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new UrgencyAssessment();

            try
            {
                return JsonSerializer.Deserialize<UrgencyAssessment>(response) ?? new UrgencyAssessment();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse urgency assessment response");
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

    public class ComprehensiveWellnessAnalysis
    {
        public WellnessAnalysisResult WellnessAnalysis { get; set; } = new();
        public List<string> SuggestedTasks { get; set; } = new();
        public UrgencyAssessment UrgencyAssessment { get; set; } = new();
        public DateTime AnalysisTimestamp { get; set; }
    }
}

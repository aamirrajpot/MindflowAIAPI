using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Utilities;
using Mindflow_Web_API.Exceptions;
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
                stream = false // <-- IMPORTANT!
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
    }
}

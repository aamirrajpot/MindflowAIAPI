using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Services
{
    public interface ITinyLlamaService
    {
        /// <summary>
        /// Simple text prediction / completion using TinyLlama on RunPod.
        /// You pass a prompt (or partial text) and get back the generated continuation as plain text.
        /// </summary>
        Task<string> PredictAsync(string prompt, int maxTokens = 64, double temperature = 0.7);
    }

    public class TinyLlamaService : ITinyLlamaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TinyLlamaService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _apiKey;
        private readonly string _endpoint;

        public TinyLlamaService(HttpClient httpClient, IConfiguration configuration, ILogger<TinyLlamaService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;

            // Allow reuse of the main RunPod API key if a dedicated one is not configured
            _apiKey = _configuration["RunPodTinyLlama:ApiKey"]
                      ?? _configuration["RunPod:ApiKey"]
                      ?? throw new InvalidOperationException("RunPodTinyLlama or RunPod API Key not configured");

            _endpoint = _configuration["RunPodTinyLlama:Endpoint"]
                        ?? throw new InvalidOperationException("RunPodTinyLlama Endpoint not configured");

            _httpClient.BaseAddress = new Uri(_endpoint);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MindflowWebAPI-TinyLlama/1.0");

            var timeoutMinutes = _configuration.GetValue<int>("RunPodTinyLlama:TimeoutMinutes", 5);
            _httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        }

        public async Task<string> PredictAsync(string prompt, int maxTokens = 64, double temperature = 0.7)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt is required", nameof(prompt));

            var cachingEnabled = _configuration.GetValue<bool>("RunPodTinyLlama:EnableCache", true);
            var cacheSeconds = _configuration.GetValue<int>("RunPodTinyLlama:CacheSeconds", 300);
            var cacheKey = $"tinyllama:{maxTokens}:{temperature}:{prompt.GetHashCode()}";

            if (cachingEnabled && _cache.TryGetValue(cacheKey, out string? cached))
            {
                if (cached != null)
                {
                    _logger.LogDebug("TinyLlama cache hit for key {Key}", cacheKey);
                    return cached;
                }
            }

            var maxRetries = _configuration.GetValue<int>("RunPodTinyLlama:MaxRetries", 3);
            var retryDelayMs = _configuration.GetValue<int>("RunPodTinyLlama:RetryDelayMs", 1000);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Reuse the same RunPod request schema as other Llama calls
                    var requestObj = LlamaPromptBuilderForRunpod.BuildRunpodRequest(prompt, maxTokens, temperature);
                    var jsonRequest = JsonSerializer.Serialize(requestObj);

                    _logger.LogInformation("Sending TinyLlama request to RunPod (attempt {Attempt}/{Max})", attempt, maxRetries);
                    _logger.LogDebug("TinyLlama request payload: {Payload}", jsonRequest);

                    var content = new StringContent(jsonRequest, Encoding.UTF8);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    var response = await _httpClient.PostAsync("", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("TinyLlama RunPod error (attempt {Attempt}): {Status} - {Content}", attempt, response.StatusCode, errorContent);

                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                            throw new HttpRequestException($"TinyLlama RunPod error: {response.StatusCode} - {errorContent}");

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelayMs);
                            continue;
                        }

                        throw new HttpRequestException($"TinyLlama RunPod error after {maxRetries} attempts: {response.StatusCode} - {errorContent}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("TinyLlama RunPod raw response: {Response}", responseContent);

                    // Parse into text (TinyLlama RunPod schema)
                    string text;
                    try
                    {
                        var tiny = JsonSerializer.Deserialize<TinyLlamaRunpodResponse>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        // Primary: continuation field from output
                        text = tiny?.Output?.Continuation 
                               ?? tiny?.Output?.Input 
                               ?? responseContent;
                    }
                    catch
                    {
                        // Fallback to raw response if schema parsing fails
                        text = responseContent;
                    }

                    if (cachingEnabled)
                    {
                        _cache.Set(cacheKey, text, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(5, cacheSeconds))
                        });
                    }

                    return text;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "TinyLlama RunPod request failed on attempt {Attempt}, retrying...", attempt);
                    await Task.Delay(retryDelayMs);
                }
            }

            throw new InvalidOperationException("TinyLlama RunPod request failed after all retry attempts");
        }
    }

    // TinyLlama-specific RunPod response DTO
    // Example:
    // {
    //   "delayTime": 13852,
    //   "executionTime": 687,
    //   "id": "sync-...",
    //   "output": {
    //     "continuation": "can't seem to find the right balance...",
    //     "input": "I feel stressed and I"
    //   },
    //   "status": "COMPLETED",
    //   "workerId": "..."
    // }
    public class TinyLlamaRunpodResponse
    {
        public TinyLlamaOutput? Output { get; set; }
    }

    public class TinyLlamaOutput
    {
        public string? Continuation { get; set; }
        public string? Input { get; set; }
    }
}



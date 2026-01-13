using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
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

        /// <summary>
        /// Text prediction / completion using the main Llama 2 chat model on RunPod
        /// (same model used by the rest of the system via <see cref="IRunPodService" />).
        /// This is intended for higher quality outputs than TinyLlama.
        /// </summary>
        Task<string> PredictWithLlama2Async(string prompt, int maxTokens = 256, double temperature = 0.7);
    }

    public class TinyLlamaService : ITinyLlamaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TinyLlamaService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IRunPodService _runPodService;
        private readonly string _apiKey;
        private readonly string _endpoint;

        public TinyLlamaService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<TinyLlamaService> logger,
            IMemoryCache cache,
            IRunPodService runPodService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
            _runPodService = runPodService;

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

        /// <summary>
        /// Uses the main RunPod Llama 2 chat model (configured under "RunPod:*" in appsettings)
        /// to generate a continuation for the given prompt.
        /// This method is tuned for short sentence completions (1–3 words).
        /// </summary>
        public async Task<string> PredictWithLlama2Async(string prompt, int maxTokens = 256, double temperature = 0.7)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt is required", nameof(prompt));

            // Use a separate cache key so TinyLlama and Llama2 don't collide
            var cachingEnabled = _configuration.GetValue<bool>("RunPodLlama2:EnableCache", true);
            var cacheSeconds = _configuration.GetValue<int>("RunPodLlama2:CacheSeconds", 600);
            var cacheKey = $"llama2:{maxTokens}:{temperature}:{prompt.GetHashCode()}";

            if (cachingEnabled && _cache.TryGetValue(cacheKey, out string? cached))
            {
                if (cached != null)
                {
                    _logger.LogDebug("Llama2 cache hit for key {Key}", cacheKey);
                    return cached;
                }
            }

            // Build a richer chat-style prompt for Llama 2 so responses behave like a short completion
            var enhancedPrompt = BuildLlama2ChatPrompt(prompt);

            // Delegate the actual RunPod call (including retries + polling) to the shared RunPodService
            var responseContent = await _runPodService.SendPromptAsync(enhancedPrompt, maxTokens, temperature);
            _logger.LogDebug("Llama2 RunPod raw response (via RunPodService): {Response}", responseContent);

            // Parse the standard RunPod Llama2 schema (handles both new and old structures)
            string text;
            try
            {
                text = RunpodResponseHelper.ExtractTextFromRunpodResponse(responseContent);
                
                // If extraction failed, fallback to raw response
                if (string.IsNullOrWhiteSpace(text) || text == responseContent)
                {
                    text = responseContent;
                }
            }
            catch
            {
                // Fallback to raw response if schema parsing fails
                text = responseContent;
            }

            // Normalize to a very short completion (1–3 words) so the caller gets just the continuation,
            // not a full paragraph or explanation.
            text = NormalizeShortCompletion(text);

            if (cachingEnabled)
            {
                _cache.Set(cacheKey, text, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(10, cacheSeconds))
                });
            }

            return text;
        }

        /// <summary>
        /// Wraps a raw user prompt in an instruction that makes Llama 2 behave like
        /// a sentence-completion model: it should naturally continue the user's text
        /// based on context, without extra explanation.
        /// </summary>
        private static string BuildLlama2ChatPrompt(string userPrompt)
        {
            var safePrompt = userPrompt?.Trim() ?? string.Empty;

            // Instruct Llama 2 to continue the fragment as a natural sentence,
            // not to explain or add meta text.
            var prompt =
                "[INST] The user will give you the **beginning of a sentence** (often about how they feel or what is happening).\n" +
                "Your task is to **naturally continue that sentence** in a way that fits the situation and sounds human.\n\n" +
                "Rules:\n" +
                "- Reply with **only the continuation of the sentence**, not a new sentence explaining it.\n" +
                "- You may use several words, but keep it concise (ideally one short sentence or fragment).\n" +
                "- Do **not** repeat the user's text.\n" +
                "- Do **not** add commentary like \"you might be\" or \"it seems\"—just complete the thought.\n\n" +
                $"User text: {safePrompt}\n\n" +
                "Sentence continuation: [/INST]";

            return prompt;
        }

        /// <summary>
        /// Post-processes the raw Llama 2 output into a clean, short completion.
        /// Trims whitespace, strips obvious punctuation, and limits the number of words
        /// so it behaves like a sentence continuation rather than a long paragraph.
        /// </summary>
        private static string NormalizeShortCompletion(string raw, int maxWords = 20)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var text = raw.Replace("\r", " ").Replace("\n", " ").Trim();

            // Remove simple wrapping quotes
            if ((text.StartsWith("\"") && text.EndsWith("\"")) ||
                (text.StartsWith("'") && text.EndsWith("'")))
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            // Trim common trailing punctuation
            text = text.Trim().Trim('.', '!', '?');

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return string.Empty;

            if (parts.Length > maxWords)
            {
                text = string.Join(" ", parts.Take(maxWords));
            }
            else
            {
                text = string.Join(" ", parts);
            }

            return text;
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



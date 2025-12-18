using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;

namespace Mindflow_Web_API.Services
{
    public class OpenAIService : IOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public OpenAIService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenAIService> logger,
            IMemoryCache cache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;

            _apiKey = _configuration["OpenAI:ApiKey"]
                     ?? throw new InvalidOperationException("OpenAI API Key not configured");

            _baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
            
            // BaseAddress is set in Program.cs via AddHttpClient configuration
            // Only set headers here
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var timeoutMinutes = _configuration.GetValue<int>("OpenAI:TimeoutMinutes", 5);
            _httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        }

        public async Task<string> CompleteAsync(string prompt, string model = "gpt-4.1-mini", int maxTokens = 64, double temperature = 0.7)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt is required", nameof(prompt));

            var cachingEnabled = _configuration.GetValue<bool>("OpenAI:EnableCache", true);
            var cacheSeconds = _configuration.GetValue<int>("OpenAI:CacheSeconds", 300);
            var cacheKey = $"openai:{model}:{maxTokens}:{temperature}:{prompt.GetHashCode()}";

            if (cachingEnabled && _cache.TryGetValue(cacheKey, out string? cached))
            {
                if (cached != null)
                {
                    _logger.LogDebug("OpenAI cache hit for key {Key}", cacheKey);
                    return cached;
                }
            }

            var maxRetries = _configuration.GetValue<int>("OpenAI:MaxRetries", 3);
            var retryDelayMs = _configuration.GetValue<int>("OpenAI:RetryDelayMs", 1000);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Add instructions to complete the sentence naturally in English without conversational response
                    var requestObj = new
                    {
                        model = model,
                        input = prompt,
                        instructions = "You are a sentence completion model. Complete the given sentence fragment naturally in ENGLISH only. If the fragment is misspelled or not a real English word, infer the most likely intended English word or phrase and use that as the completion. Return ONLY the continuation text (no quotes, no translation notes, no language commentary). Do not ask questions, do not explain, do not respond in any language other than English.",
                        max_output_tokens = maxTokens,
                        temperature = temperature
                    };

                    var jsonRequest = JsonSerializer.Serialize(requestObj);
                    _logger.LogInformation("Sending OpenAI request (attempt {Attempt}/{Max})", attempt, maxRetries);
                    _logger.LogDebug("OpenAI request payload: {Payload}", jsonRequest);

                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    
                    // Log the BaseAddress for debugging
                    _logger.LogDebug("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress?.ToString() ?? "Not set");
                    
                    var response = await _httpClient.PostAsync("responses", content);
                    
                    // Log the actual request URI for debugging
                    _logger.LogDebug("Request URI: {RequestUri}", response.RequestMessage?.RequestUri?.ToString() ?? "Unknown");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("OpenAI API error (attempt {Attempt}): {Status} - {Content}", attempt, response.StatusCode, errorContent);

                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                            throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {errorContent}");

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelayMs);
                            continue;
                        }

                        throw new HttpRequestException($"OpenAI API error after {maxRetries} attempts: {response.StatusCode} - {errorContent}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("OpenAI API raw response: {Response}", responseContent);

                    // Parse OpenAI Responses API response
                    string text;
                    try
                    {
                        var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        var rawText = openAiResponse?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text 
                               ?? throw new InvalidOperationException("No content in OpenAI response");
                        
                        // Post-process: Extract only the sentence completion
                        text = ExtractSentenceCompletion(prompt, rawText);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse OpenAI response");
                        throw new InvalidOperationException($"Failed to parse OpenAI response: {ex.Message}", ex);
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
                    _logger.LogWarning(ex, "OpenAI API request failed on attempt {Attempt}, retrying...", attempt);
                    await Task.Delay(retryDelayMs);
                }
            }

            throw new InvalidOperationException("OpenAI API request failed after all retry attempts");
        }

        public async Task<string> CompleteWithSystemMessageAsync(string systemMessage, string userPrompt, string model = "gpt-4.1-mini", int maxTokens = 64, double temperature = 0.7)
        {
            if (string.IsNullOrWhiteSpace(systemMessage))
                throw new ArgumentException("System message is required", nameof(systemMessage));
            
            if (string.IsNullOrWhiteSpace(userPrompt))
                throw new ArgumentException("User prompt is required", nameof(userPrompt));

            // Combine system message and user prompt into a single prompt for Completions API
            var combinedPrompt = $"{systemMessage}\n\n{userPrompt}";

            var cachingEnabled = _configuration.GetValue<bool>("OpenAI:EnableCache", true);
            var cacheSeconds = _configuration.GetValue<int>("OpenAI:CacheSeconds", 300);
            var cacheKey = $"openai:{model}:{maxTokens}:{temperature}:system:{systemMessage.GetHashCode()}:user:{userPrompt.GetHashCode()}";

            if (cachingEnabled && _cache.TryGetValue(cacheKey, out string? cached))
            {
                if (cached != null)
                {
                    _logger.LogDebug("OpenAI cache hit for key {Key}", cacheKey);
                    return cached;
                }
            }

            var maxRetries = _configuration.GetValue<int>("OpenAI:MaxRetries", 3);
            var retryDelayMs = _configuration.GetValue<int>("OpenAI:RetryDelayMs", 1000);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Add instructions to complete the sentence naturally in English without conversational response
                    var requestObj = new
                    {
                        model = model,
                        input = combinedPrompt,
                        instructions = "You are a sentence completion model. Complete the given sentence fragment naturally in ENGLISH only. If the fragment is misspelled or not a real English word, infer the most likely intended English word or phrase and use that as the completion. Return ONLY the continuation text (no quotes, no translation notes, no language commentary). Do not ask questions, do not explain, do not respond in any language other than English.",
                        max_output_tokens = maxTokens,
                        temperature = temperature
                    };

                    var jsonRequest = JsonSerializer.Serialize(requestObj);
                    _logger.LogInformation("Sending OpenAI request with system message (attempt {Attempt}/{Max})", attempt, maxRetries);
                    _logger.LogDebug("OpenAI request payload: {Payload}", jsonRequest);

                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    
                    // Log the BaseAddress for debugging
                    _logger.LogDebug("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress?.ToString() ?? "Not set");
                    
                    var response = await _httpClient.PostAsync("responses", content);
                    
                    // Log the actual request URI for debugging
                    _logger.LogDebug("Request URI: {RequestUri}", response.RequestMessage?.RequestUri?.ToString() ?? "Unknown");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("OpenAI API error (attempt {Attempt}): {Status} - {Content}", attempt, response.StatusCode, errorContent);

                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                            throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {errorContent}");

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelayMs);
                            continue;
                        }

                        throw new HttpRequestException($"OpenAI API error after {maxRetries} attempts: {response.StatusCode} - {errorContent}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("OpenAI API raw response: {Response}", responseContent);

                    // Parse OpenAI Responses API response
                    string text;
                    try
                    {
                        var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        var rawText = openAiResponse?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text 
                               ?? throw new InvalidOperationException("No content in OpenAI response");
                        
                        // Post-process: Extract only the sentence completion
                        text = ExtractSentenceCompletion(combinedPrompt, rawText);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse OpenAI response");
                        throw new InvalidOperationException($"Failed to parse OpenAI response: {ex.Message}", ex);
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
                    _logger.LogWarning(ex, "OpenAI API request failed on attempt {Attempt}, retrying...", attempt);
                    await Task.Delay(retryDelayMs);
                }
            }

            throw new InvalidOperationException("OpenAI API request failed after all retry attempts");
        }

        /// <summary>
        /// Extracts only the sentence completion from the AI response.
        /// If the response includes the original prompt, extracts just the continuation.
        /// Otherwise, returns the response as-is (assuming the model followed instructions).
        /// </summary>
        private static string ExtractSentenceCompletion(string originalPrompt, string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
                return string.Empty;

            var response = aiResponse.Trim();
            var promptTrimmed = originalPrompt.Trim();

            // If response starts with the prompt, extract just the continuation part
            if (response.StartsWith(promptTrimmed, StringComparison.OrdinalIgnoreCase))
            {
                var completion = response.Substring(promptTrimmed.Length).Trim();
                // Take only the first sentence/phrase (up to punctuation or newline)
                var endIndex = completion.IndexOfAny(new[] { '.', '?', '!', '\n' });
                if (endIndex > 0)
                {
                    completion = completion.Substring(0, endIndex).Trim();
                }
                return completion;
            }

            // Otherwise, assume the model followed instructions and returned just the completion
            // Take first sentence/phrase up to punctuation
            var firstSentenceEnd = response.IndexOfAny(new[] { '.', '?', '!', '\n' });
            if (firstSentenceEnd > 0)
            {
                return response.Substring(0, firstSentenceEnd).Trim();
            }

            // Return the response as-is if no sentence-ending punctuation found
            return response;
        }
    }

    // OpenAI Responses API response DTOs
    public class OpenAIResponse
    {
        public List<OpenAIOutput>? Output { get; set; }
    }

    public class OpenAIOutput
    {
        public List<OpenAIContent>? Content { get; set; }
    }

    public class OpenAIContent
    {
        public string? Text { get; set; }
        public string? Type { get; set; }
    }
}


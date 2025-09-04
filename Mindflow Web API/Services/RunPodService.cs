using System.Text;
using System.Text.Json;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Services
{
    public interface IRunPodService
    {
        Task<RunPodResponse> AnalyzeWellnessAsync(WellnessCheckIn checkIn, int maxTokens = 1000, double temperature = 0.7);
        Task<List<TaskSuggestion>> GetTaskSuggestionsAsync(WellnessCheckIn checkIn, int maxTokens = 1000, double temperature = 0.7);
        Task<UrgencyAssessment> AssessUrgencyAsync(WellnessCheckIn checkIn, int maxTokens = 1000, double temperature = 0.7);
        Task<string> SendPromptAsync(string prompt, int maxTokens = 1000, double temperature = 0.7);
    }

    public class RunPodService : IRunPodService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RunPodService> _logger;
        private readonly string _runpodApiKey;
        private readonly string _runpodEndpoint;

        public RunPodService(HttpClient httpClient, IConfiguration configuration, ILogger<RunPodService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            // Get RunPod configuration from appsettings
            _runpodApiKey = _configuration["RunPod:ApiKey"] ?? throw new InvalidOperationException("RunPod API Key not configured");
            _runpodEndpoint = _configuration["RunPod:Endpoint"] ?? throw new InvalidOperationException("RunPod Endpoint not configured");
            
            // Configure HttpClient for RunPod
            _httpClient.BaseAddress = new Uri(_runpodEndpoint);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_runpodApiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MindflowWebAPI/1.0");
            
            // Configure timeouts for RunPod calls (can be long-running)
            var timeoutMinutes = _configuration.GetValue<int>("RunPod:TimeoutMinutes", 10);
            _httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        }

        public async Task<RunPodResponse> AnalyzeWellnessAsync(WellnessCheckIn checkIn, int maxTokens = 1000, double temperature = 0.7)
        {
            try
            {
                var prompt = LlamaPromptBuilderForRunpod.BuildWellnessPromptForRunpod(checkIn);
                var response = await SendPromptAsync(prompt, maxTokens, temperature);
                
                // Parse the response to extract wellness analysis
                var wellnessAnalysis = ParseWellnessAnalysis(response);
                return wellnessAnalysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing wellness with RunPod");
                throw;
            }
        }

        public async Task<List<TaskSuggestion>> GetTaskSuggestionsAsync(WellnessCheckIn checkIn, int maxTokens = 1000, double temperature = 0.7)
        {
            try
            {
                var prompt = LlamaPromptBuilderForRunpod.BuildTaskSuggestionPromptForRunpod(checkIn);
                var response = await SendPromptAsync(prompt, maxTokens, temperature);
                
                // Parse the response to extract task suggestions
                var tasks = LlamaPromptBuilderForRunpod.ParseTaskSuggestions(response);
                return tasks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task suggestions with RunPod");
                throw;
            }
        }

        public async Task<UrgencyAssessment> AssessUrgencyAsync(WellnessCheckIn checkIn, int maxTokens = 1000, double temperature = 0.7)
        {
            try
            {
                var prompt = LlamaPromptBuilderForRunpod.BuildUrgencyAssessmentPromptForRunpod(checkIn);
                var response = await SendPromptAsync(prompt, maxTokens, temperature);
                
                // Parse the response to extract urgency assessment
                var urgencyAssessment = ParseUrgencyAssessment(response);
                return urgencyAssessment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assessing urgency with RunPod");
                throw;
            }
        }

        public async Task<string> SendPromptAsync(string prompt, int maxTokens = 1000, double temperature = 0.7)
        {
            var maxRetries = _configuration.GetValue<int>("RunPod:MaxRetries", 3);
            var retryDelayMs = _configuration.GetValue<int>("RunPod:RetryDelayMs", 2000);
            var timeoutMinutes = _configuration.GetValue<int>("RunPod:TimeoutMinutes", 10);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Build the RunPod request
                    var request = LlamaPromptBuilderForRunpod.BuildRunpodRequest(prompt, maxTokens, temperature);
                    var jsonRequest = JsonSerializer.Serialize(request);
                    
                    _logger.LogInformation("Sending request to RunPod (attempt {Attempt}/{MaxRetries}): {Endpoint}", 
                        attempt, maxRetries, _runpodEndpoint);
                    _logger.LogDebug("Request payload: {Payload}", jsonRequest);

                    // Create HTTP content
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    
                    // Send the request to RunPod with cancellation token for timeout handling
                    using var cts = new CancellationTokenSource();
                    var requestTimeoutMinutes = timeoutMinutes - 2; // 2 minutes less than HttpClient timeout
                    cts.CancelAfter(TimeSpan.FromMinutes(requestTimeoutMinutes));
                    
                    var response = await _httpClient.PostAsync("", content, cts.Token);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("RunPod API error (attempt {Attempt}): {StatusCode} - {Content}", 
                            attempt, response.StatusCode, errorContent);
                        
                        // Don't retry on client errors (4xx)
                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                        {
                            throw new HttpRequestException($"RunPod API error: {response.StatusCode} - {errorContent}");
                        }
                        
                        // Retry on server errors (5xx) or other errors
                        if (attempt < maxRetries)
                        {
                            _logger.LogWarning("Retrying in {Delay}ms due to server error...", retryDelayMs);
                            await Task.Delay(retryDelayMs, cts.Token);
                            continue;
                        }
                        
                        throw new HttpRequestException($"RunPod API error after {maxRetries} attempts: {response.StatusCode} - {errorContent}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("RunPod response (attempt {Attempt}): {Response}", attempt, responseContent);
                    _logger.LogInformation("Successfully received response from RunPod after {Attempt} attempt(s)", attempt);

                    return responseContent;
                }
                catch (OperationCanceledException) when (attempt < maxRetries)
                {
                    _logger.LogWarning("Request timeout on attempt {Attempt}, retrying...", attempt);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    throw new TimeoutException($"RunPod request timed out after {maxRetries} attempts");
                }
                catch (HttpRequestException) when (attempt < maxRetries)
                {
                    // Already logged above, just retry
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogError(ex, "Unexpected error on attempt {Attempt}, retrying...", attempt);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    throw;
                }
            }

            // This should never be reached, but just in case
            throw new InvalidOperationException("Unexpected error in retry loop");
        }

        private RunPodResponse ParseWellnessAnalysis(string response)
        {
            try
            {
                // Parse the RunPod response structure
                var runpodResponse = JsonSerializer.Deserialize<RunpodResponse>(response);
                if (runpodResponse?.Output?.FirstOrDefault()?.Choices?.FirstOrDefault()?.Tokens == null)
                {
                    _logger.LogWarning("No valid response structure found in RunPod response");
                    return new RunPodResponse();
                }

                var tokens = runpodResponse.Output.First().Choices.First().Tokens;
                var fullText = string.Join("", tokens);

                // Extract the JSON content from the response
                var jsonStart = fullText.IndexOf('{');
                var jsonEnd = fullText.LastIndexOf('}');
                
                if (jsonStart == -1 || jsonEnd == -1)
                {
                    _logger.LogWarning("No JSON content found in RunPod response");
                    return new RunPodResponse();
                }

                var jsonContent = fullText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                // Try to parse as wellness analysis
                var wellnessAnalysis = JsonSerializer.Deserialize<WellnessAnalysis>(jsonContent);
                
                return new RunPodResponse
                {
                    WellnessAnalysis = wellnessAnalysis,
                    RawResponse = response
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing wellness analysis from RunPod response");
                return new RunPodResponse { RawResponse = response };
            }
        }

        private UrgencyAssessment ParseUrgencyAssessment(string response)
        {
            try
            {
                // Parse the RunPod response structure
                var runpodResponse = JsonSerializer.Deserialize<RunpodResponse>(response);
                if (runpodResponse?.Output?.FirstOrDefault()?.Choices?.FirstOrDefault()?.Tokens == null)
                {
                    _logger.LogWarning("No valid response structure found in RunPod response");
                    return new UrgencyAssessment();
                }

                var tokens = runpodResponse.Output.First().Choices.First().Tokens;
                var fullText = string.Join("", tokens);

                // Extract the JSON content from the response
                var jsonStart = fullText.IndexOf('{');
                var jsonEnd = fullText.LastIndexOf('}');
                
                if (jsonStart == -1 || jsonEnd == -1)
                {
                    _logger.LogWarning("No JSON content found in RunPod response");
                    return new UrgencyAssessment();
                }

                var jsonContent = fullText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                // Parse as urgency assessment
                var urgencyAssessment = JsonSerializer.Deserialize<UrgencyAssessment>(jsonContent);
                return urgencyAssessment ?? new UrgencyAssessment();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing urgency assessment from RunPod response");
                return new UrgencyAssessment();
            }
        }
    }

    // Response models for the service
    public class RunPodResponse
    {
        public WellnessAnalysis? WellnessAnalysis { get; set; }
        public List<TaskSuggestion>? TaskSuggestions { get; set; }
        public UrgencyAssessment? UrgencyAssessment { get; set; }
        public string? RawResponse { get; set; }
        public bool IsSuccess => WellnessAnalysis != null || TaskSuggestions != null || UrgencyAssessment != null;
    }

    public class WellnessAnalysis
    {
        public string? MoodAssessment { get; set; }
        public string? StressLevel { get; set; }
        public List<string>? SupportNeeds { get; set; }
        public List<string>? CopingStrategies { get; set; }
        public List<string>? SelfCareSuggestions { get; set; }
        public string? ProgressTracking { get; set; }
        public int UrgencyLevel { get; set; }
        public List<string>? ImmediateActions { get; set; }
        public List<string>? LongTermGoals { get; set; }
    }

    public class UrgencyAssessment
    {
        public int UrgencyLevel { get; set; }
        public string? Reasoning { get; set; }
        public string? ImmediateAction { get; set; }
    }
}

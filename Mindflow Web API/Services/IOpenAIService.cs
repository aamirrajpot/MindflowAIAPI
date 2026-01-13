namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Service interface for interacting with OpenAI's API to complete user input.
    /// </summary>
    public interface IOpenAIService
    {
        /// <summary>
        /// Completes user input using OpenAI's Completions API.
        /// </summary>
        /// <param name="prompt">The user's input/prompt to complete</param>
        /// <param name="model">The OpenAI model to use (default: gpt-3.5-turbo-instruct)</param>
        /// <param name="maxTokens">Maximum number of tokens in the response</param>
        /// <param name="temperature">Sampling temperature (0.0 to 2.0). Higher values make output more random.</param>
        /// <param name="context">Optional context key indicating which input field is being completed (e.g. "productivity goal", "emotion", "title", "task")</param>
        /// <returns>The completed text from OpenAI</returns>
        Task<string> CompleteAsync(string prompt, string model = "gpt-4.1-mini", int maxTokens = 64, double temperature = 0.7, string? context = null);

        /// <summary>
        /// Completes user input with a system message for context using OpenAI's Completions API.
        /// </summary>
        /// <param name="systemMessage">System message to set the behavior of the assistant</param>
        /// <param name="userPrompt">The user's input/prompt to complete</param>
        /// <param name="model">The OpenAI model to use (default: gpt-3.5-turbo-instruct)</param>
        /// <param name="maxTokens">Maximum number of tokens in the response</param>
        /// <param name="temperature">Sampling temperature (0.0 to 2.0)</param>
        /// <param name="context">Optional context key indicating which input field is being completed</param>
        /// <returns>The completed text from OpenAI</returns>
        Task<string> CompleteWithSystemMessageAsync(string systemMessage, string userPrompt, string model = "gpt-4.1-mini", int maxTokens = 64, double temperature = 0.7, string? context = null);
    }
}


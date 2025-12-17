using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Mindflow_Web_API.EndPoints
{
    public static class OpenAIEndpoints
    {
        public static void MapOpenAIEndpoints(this IEndpointRouteBuilder app)
        {
            var openAiApi = app.MapGroup("/api/openai").WithTags("OpenAI");

            // Health check endpoint (public)
            openAiApi.MapGet("/health", () =>
            {
                return Results.Ok(new
                {
                    service = "OpenAI Service",
                    status = "Healthy",
                    timestamp = DateTime.UtcNow,
                    features = new[]
                    {
                        "Text Completion",
                        "Chat Completion",
                        "System Message Support"
                    }
                });
            })
            .WithOpenApi(op =>
            {
                op.Summary = "OpenAI service health check";
                op.Description = "Returns the health status of the OpenAI service";
                return op;
            });

            // Simple text completion endpoint
            openAiApi.MapPost("/complete", async (
                IOpenAIService openAIService,
                [FromBody] TextPredictionRequest request,
                [FromQuery] string model = "gpt-4.1-mini",
                [FromQuery] int maxTokens = 64,
                [FromQuery] double temperature = 0.7) =>
            {
                if (string.IsNullOrWhiteSpace(request.Prompt))
                    throw ApiExceptions.BadRequest("Prompt is required");

                var result = await openAIService.CompleteAsync(request.Prompt, model, maxTokens, temperature);
                return Results.Ok(new { completion = result });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Complete text using OpenAI";
                op.Description = "Uses OpenAI's Completions API to complete the provided prompt.";
                return op;
            });

            // Completion with system message endpoint
            openAiApi.MapPost("/complete-with-context", async (
                IOpenAIService openAIService,
                [FromBody] OpenAICompletionRequest request,
                [FromQuery] string model = "gpt-4.1-mini",
                [FromQuery] int maxTokens = 64,
                [FromQuery] double temperature = 0.7) =>
            {
                if (string.IsNullOrWhiteSpace(request.SystemMessage))
                    throw ApiExceptions.BadRequest("System message is required");

                if (string.IsNullOrWhiteSpace(request.UserPrompt))
                    throw ApiExceptions.BadRequest("User prompt is required");

                var result = await openAIService.CompleteWithSystemMessageAsync(
                    request.SystemMessage, 
                    request.UserPrompt, 
                    model, 
                    maxTokens, 
                    temperature);
                
                return Results.Ok(new { completion = result });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Complete text with system context using OpenAI";
                op.Description = "Uses OpenAI's Completions API with a system message to provide context and complete the user's prompt.";
                return op;
            });
        }
    }

    public class OpenAICompletionRequest
    {
        public string SystemMessage { get; set; } = "";
        public string UserPrompt { get; set; } = "";
    }
}


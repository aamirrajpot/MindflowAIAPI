using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.Exceptions;

namespace Mindflow_Web_API.Middleware
{
    public class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, message, errorCode) = exception switch
            {
                ApiException apiEx => (apiEx.StatusCode, apiEx.Message, apiEx.ErrorCode),
                ArgumentNullException => (HttpStatusCode.BadRequest, exception.Message, "VALIDATION_ERROR"),
                ArgumentException => (HttpStatusCode.BadRequest, exception.Message, "VALIDATION_ERROR"),
                InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message, "INVALID_OPERATION"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized access", "UNAUTHORIZED"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found", "NOT_FOUND"),
                NotImplementedException => (HttpStatusCode.NotImplemented, "Feature not implemented", "NOT_IMPLEMENTED"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Request timed out", "TIMEOUT"),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred", "INTERNAL_SERVER_ERROR")
            };

            context.Response.StatusCode = (int)statusCode;

            var errorResponse = new
            {
                StatusCode = (int)statusCode,
                Message = message,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                Method = context.Request.Method,
                RequestId = context.TraceIdentifier
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
} 
using System.Net;

namespace Mindflow_Web_API.Exceptions
{
    public class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ErrorCode { get; }

        public ApiException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? errorCode = null) 
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode ?? "API_ERROR";
        }

        public ApiException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? errorCode = null) 
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode ?? "API_ERROR";
        }
    }

    public static class ApiExceptions
    {
        public static ApiException ValidationError(string message) => 
            new ApiException(message, HttpStatusCode.BadRequest, "VALIDATION_ERROR");

        public static ApiException BadRequest(string message) => 
            new ApiException(message, HttpStatusCode.BadRequest, "BAD_REQUEST");

        public static ApiException NotFound(string message) => 
            new ApiException(message, HttpStatusCode.NotFound, "NOT_FOUND");

        public static ApiException Unauthorized(string message = "Unauthorized access") => 
            new ApiException(message, HttpStatusCode.Unauthorized, "UNAUTHORIZED");

        public static ApiException Forbidden(string message = "Access forbidden") => 
            new ApiException(message, HttpStatusCode.Forbidden, "FORBIDDEN");

        public static ApiException Conflict(string message) => 
            new ApiException(message, HttpStatusCode.Conflict, "CONFLICT");

        public static ApiException InternalServerError(string message = "An internal server error occurred") => 
            new ApiException(message, HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR");
    }
} 
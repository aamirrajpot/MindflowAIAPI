namespace Mindflow_Web_API.DTOs
{
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T? Data { get; set; }
        public object? Error { get; set; }

        public ApiResponseDto(bool success, string message, T? data = default, object? error = null)
        {
            Success = success;
            Message = message;
            Data = data;
            Error = error;
        }
    }
} 
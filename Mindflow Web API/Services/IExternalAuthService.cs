using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IExternalAuthService
    {
        Task<ExternalAuthResponseDto?> GoogleAuthenticateAsync(GoogleAuthDto command);
    }
} 
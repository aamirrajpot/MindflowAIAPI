using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IWellnessCheckInService
    {
        Task<WellnessCheckInDto> SubmitAsync(Guid userId, CreateWellnessCheckInDto command);
        Task<WellnessCheckInDto?> GetAsync(Guid userId);
    }
} 
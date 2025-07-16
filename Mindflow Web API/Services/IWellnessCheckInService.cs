using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IWellnessCheckInService
    {
        Task<WellnessCheckInDto?> GetAsync(Guid userId);
        Task<WellnessCheckInDto?> PatchAsync(Guid userId, PatchWellnessCheckInDto patchDto);
    }
} 
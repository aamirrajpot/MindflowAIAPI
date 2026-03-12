using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IWellnessCheckInService
    {
        Task<WellnessCheckInDto?> GetAsync(Guid userId);
        Task<WellnessCheckInDto?> PatchAsync(Guid userId, PatchWellnessCheckInDto patchDto);
        Task<WellnessCheckInDto?> UpdateSlotsAsync(Guid userId, UpdateWellnessSlotsDto slotsDto);
        Task<WellnessSummaryDto> GetWellnessSummaryAsync(Guid userId);
        Task<AnalyticsDto> GetAnalyticsAsync(Guid userId);
    }
} 
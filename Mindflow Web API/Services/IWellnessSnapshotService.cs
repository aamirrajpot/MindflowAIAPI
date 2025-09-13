using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IWellnessSnapshotService
    {
        Task<WellnessSnapshotDto> GetWellnessSnapshotAsync(Guid userId, int days = 7);
        Task<WellnessSnapshotDto> GetWellnessSnapshotForPeriodAsync(Guid userId, DateTime startDate, DateTime endDate);
    }
}

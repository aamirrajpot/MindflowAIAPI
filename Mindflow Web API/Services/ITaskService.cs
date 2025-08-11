using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface ITaskItemService
    {
        Task<TaskItemDto> CreateAsync(Guid userId, CreateTaskItemDto dto);
        Task<TaskItemDto?> GetByIdAsync(Guid userId, Guid taskId);
        Task<IEnumerable<TaskItemDto>> GetAllAsync(Guid userId, DateTime? date = null);
        Task<TaskItemDto?> UpdateAsync(Guid userId, Guid taskId, UpdateTaskItemDto dto);
        Task<bool> DeleteAsync(Guid userId, Guid taskId);
                Task<TaskItemDto?> UpdateStatusAsync(Guid userId, Guid taskId, Mindflow_Web_API.Models.TaskStatus status);

    }
}

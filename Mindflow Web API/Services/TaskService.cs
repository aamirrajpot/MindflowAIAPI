using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Exceptions;

namespace Mindflow_Web_API.Services
{
    public class TaskItemService : ITaskItemService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<TaskItemService> _logger;

        public TaskItemService(MindflowDbContext dbContext, ILogger<TaskItemService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<TaskItemDto> CreateAsync(Guid userId, CreateTaskItemDto dto)
        {
            var task = new TaskItem
            {
                UserId = userId,
                Title = dto.Title,
                Description = dto.Description,
                Category = dto.Category,
                OtherCategoryName = dto.OtherCategoryName,
                Date = dto.Date,
                Time = dto.Time,
                TimeShift = dto.TimeShift,
                DurationMinutes = dto.DurationMinutes,
                ReminderEnabled = dto.ReminderEnabled,
                RepeatType = dto.RepeatType,
                CreatedBySuggestionEngine = false,
                IsApproved = true,
                Status = dto.Status
            };
            await _dbContext.Tasks.AddAsync(task);
            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        public async Task<TaskItemDto?> GetByIdAsync(Guid userId, Guid taskId)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            return task == null ? null : ToDto(task);
        }

        public async Task<IEnumerable<TaskItemDto>> GetAllAsync(Guid userId)
        {
            return await GetAllAsync(userId, null);
        }

        public async Task<IEnumerable<TaskItemDto>> GetAllAsync(Guid userId, DateTime? date = null)
        {
            var query = _dbContext.Tasks.Where(t => t.UserId == userId);
            if (date.HasValue)
            {
                query = query.Where(t => t.Date.Date == date.Value.Date);
            }
            var tasks = await query.ToListAsync();
            return tasks.Select(ToDto);
        }

        public async Task<TaskItemDto?> UpdateAsync(Guid userId, Guid taskId, UpdateTaskItemDto dto)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return null;
            if (dto.Title != null) task.Title = dto.Title;
            if (dto.Description != null) task.Description = dto.Description;
            if (dto.Category.HasValue) task.Category = dto.Category.Value;
            if (dto.OtherCategoryName != null) task.OtherCategoryName = dto.OtherCategoryName;
            if (dto.Date.HasValue) task.Date = dto.Date.Value;
            if (dto.Time != null) task.Time = dto.Time;
            if (dto.TimeShift != null) task.TimeShift = dto.TimeShift;
            if (dto.DurationMinutes.HasValue) task.DurationMinutes = dto.DurationMinutes.Value;
            if (dto.ReminderEnabled.HasValue) task.ReminderEnabled = dto.ReminderEnabled.Value;
            if (dto.RepeatType.HasValue) task.RepeatType = dto.RepeatType.Value;
            if (dto.IsApproved.HasValue) task.IsApproved = dto.IsApproved.Value;
            if (dto.Status.HasValue) task.Status = dto.Status.Value;
            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        public async Task<TaskItemDto?> UpdateStatusAsync(Guid userId, Guid taskId, Mindflow_Web_API.Models.TaskStatus status)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return null;
            task.Status = status;
            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid taskId)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return false;
            _dbContext.Tasks.Remove(task);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        private static TaskItemDto ToDto(TaskItem task) => new(
            task.Id,
            task.UserId,
            task.Title,
            task.Description,
            task.Category,
            task.OtherCategoryName,
            task.Date,
            task.Time,
            task.TimeShift,
            task.DurationMinutes,
            task.ReminderEnabled,
            task.RepeatType,
            task.CreatedBySuggestionEngine,
            task.IsApproved,
            task.Status
        );
    }
}

using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                Time = EnsureUtc(dto.Time),
                DurationMinutes = dto.DurationMinutes,
                ReminderEnabled = dto.ReminderEnabled,
                RepeatType = dto.RepeatType,
                CreatedBySuggestionEngine = false,
                IsApproved = true,
                Status = dto.Status,
                // Recurring task fields
                ParentTaskId = dto.ParentTaskId,
                IsTemplate = dto.IsTemplate,
                NextOccurrence = dto.NextOccurrence,
                MaxOccurrences = dto.MaxOccurrences,
                EndDate = dto.EndDate,
                IsActive = dto.IsActive
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

        public async Task<IEnumerable<TaskItemDto>> GetAllAsync(Guid userId, DateTime? date = null)
        {
            List<TaskItem> tasks;
            
            if (date.HasValue)
            {
                // Use raw SQL for date filtering
                tasks = await _dbContext.Tasks
                    .FromSqlRaw(@"
                        SELECT * FROM Tasks 
                        WHERE UserId = {0} 
                        AND DATE(Date) = DATE({1})", userId, date.Value)
                    .ToListAsync();
            }
            else
            {
                tasks = await _dbContext.Tasks
                    .Where(t => t.UserId == userId)
                    .ToListAsync();
            }
            
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
            if (dto.Time.HasValue) 
            {
                task.Time = EnsureUtc(dto.Time.Value);
            }
            if (dto.DurationMinutes.HasValue) task.DurationMinutes = dto.DurationMinutes.Value;
            if (dto.ReminderEnabled.HasValue) task.ReminderEnabled = dto.ReminderEnabled.Value;
            if (dto.RepeatType.HasValue) task.RepeatType = dto.RepeatType.Value;
            if (dto.IsApproved.HasValue) task.IsApproved = dto.IsApproved.Value;
            if (dto.Status.HasValue) task.Status = dto.Status.Value;
            
            // Recurring task fields
            if (dto.ParentTaskId.HasValue) task.ParentTaskId = dto.ParentTaskId.Value;
            if (dto.IsTemplate.HasValue) task.IsTemplate = dto.IsTemplate.Value;
            if (dto.NextOccurrence.HasValue) task.NextOccurrence = dto.NextOccurrence.Value;
            if (dto.MaxOccurrences.HasValue) task.MaxOccurrences = dto.MaxOccurrences.Value;
            if (dto.EndDate.HasValue) task.EndDate = dto.EndDate.Value;
            if (dto.IsActive.HasValue) task.IsActive = dto.IsActive.Value;

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

        public async Task<TaskItemDto?> UpdateStatusAsync(Guid userId, Guid taskId, Mindflow_Web_API.Models.TaskStatus status)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return null;
            task.Status = status;
            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        private static TaskItemDto ToDto(TaskItem task)
        {
            return new TaskItemDto(
                task.Id,
                task.UserId,
                task.Title,
                task.Description,
                task.Category,
                task.OtherCategoryName,
                task.Date,
                EnsureUtc(task.Time),
                task.DurationMinutes,
                task.ReminderEnabled,
                task.RepeatType,
                task.CreatedBySuggestionEngine,
                task.IsApproved,
                task.Status,
                // Recurring task fields
                task.ParentTaskId,
                task.IsTemplate,
                task.NextOccurrence,
                task.MaxOccurrences,
                task.EndDate,
                task.IsActive
            );
        }

        private static DateTime EnsureUtc(DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) 
                : dateTime.ToUniversalTime();
        }

        // Recurring task methods
        public async Task<IEnumerable<TaskItemDto>> GetTasksWithRecurringAsync(Guid userId, DateTime date)
        {
            // 1. Get existing tasks for the date
            var existingTasks = await GetAllAsync(userId, date);
            
            // 2. Get active templates that should have instances for this date
            var templates = await GetActiveTemplatesAsync(userId);
            
            // 3. Generate missing instances
            var newInstances = new List<TaskItem>();
            foreach (var template in templates)
            {
                if (ShouldGenerateInstanceForDate(template, date) && 
                    !await HasInstanceForDateAsync(template.Id, date))
                {
                    var instance = await GenerateTaskInstanceAsync(template.Id, date);
                    newInstances.Add(await GetTaskItemByIdAsync(instance.Id));
                }
            }
            
            // 4. Return all tasks (existing + new instances)
            return existingTasks.Concat(newInstances.Select(ToDto));
        }

        public async Task<IEnumerable<TaskItemDto>> GetActiveTemplatesAsync(Guid userId)
        {
            var templates = await _dbContext.Tasks
                .Where(t => t.UserId == userId && t.IsTemplate && t.IsActive)
                .ToListAsync();
            
            return templates.Select(ToDto);
        }

        public async Task<bool> HasInstanceForDateAsync(Guid templateId, DateTime date)
        {
            return await _dbContext.Tasks
                .AnyAsync(t => t.ParentTaskId == templateId && t.Date.Date == date.Date);
        }

        public async Task<TaskItemDto> GenerateTaskInstanceAsync(Guid templateId, DateTime date)
        {
            var template = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == templateId);
            if (template == null)
                throw new ArgumentException("Template not found");

            var instance = new TaskItem
            {
                UserId = template.UserId,
                Title = template.Title,
                Description = template.Description,
                Category = template.Category,
                OtherCategoryName = template.OtherCategoryName,
                Date = date,
                Time = date.Date.Add(template.Time.TimeOfDay), // Use same time as template
                DurationMinutes = template.DurationMinutes,
                ReminderEnabled = template.ReminderEnabled,
                RepeatType = RepeatType.Never, // Instances don't repeat
                CreatedBySuggestionEngine = template.CreatedBySuggestionEngine,
                IsApproved = template.IsApproved,
                Status = Models.TaskStatus.Pending,
                // Recurring task fields
                ParentTaskId = templateId,
                IsTemplate = false,
                IsActive = true
            };

            _dbContext.Tasks.Add(instance);
            await _dbContext.SaveChangesAsync();
            
            return ToDto(instance);
        }

        private async Task<TaskItem> GetTaskItemByIdAsync(Guid taskId)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new ArgumentException("Task not found");
            return task;
        }

        private static bool ShouldGenerateInstanceForDate(TaskItemDto template, DateTime date)
        {
            // Check if template should generate an instance for this date
            if (!template.IsTemplate || !template.IsActive)
                return false;

            // Check if date is within the template's date range
            if (template.EndDate.HasValue && date > template.EndDate.Value)
                return false;

            // Check if we've exceeded max occurrences
            if (template.MaxOccurrences.HasValue)
            {
                // This would require counting existing instances - simplified for now
                // In a full implementation, you'd count existing instances
            }

            // Check if the date matches the recurrence pattern
            return template.RepeatType switch
            {
                RepeatType.Day => true, // Daily - always generate
                RepeatType.Week => date.DayOfWeek == template.Date.DayOfWeek, // Same day of week
                RepeatType.Month => date.Day == template.Date.Day, // Same day of month
                _ => false
            };
        }
    }
}

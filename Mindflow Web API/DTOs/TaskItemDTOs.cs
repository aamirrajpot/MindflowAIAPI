using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.DTOs
{
    public record CreateTaskItemDto(
        string Title,
        string? Description,
        TaskCategory Category,
        string? OtherCategoryName,
        DateTime Date,
        DateTime Time,
        int DurationMinutes,
        bool ReminderEnabled,
        RepeatType RepeatType,
        Mindflow_Web_API.Models.TaskStatus Status,
        // Recurring task fields
        Guid? ParentTaskId = null,
        bool IsTemplate = false,
        DateTime? NextOccurrence = null,
        int? MaxOccurrences = null,
        DateTime? EndDate = null,
        bool IsActive = true
    );

    public record UpdateTaskItemDto(
        string? Title,
        string? Description,
        TaskCategory? Category,
        string? OtherCategoryName,
        DateTime? Date,
        DateTime? Time,
        int? DurationMinutes,
        bool? ReminderEnabled,
        RepeatType? RepeatType,
        bool? IsApproved,
        Mindflow_Web_API.Models.TaskStatus? Status,
        // Recurring task fields
        Guid? ParentTaskId = null,
        bool? IsTemplate = null,
        DateTime? NextOccurrence = null,
        int? MaxOccurrences = null,
        DateTime? EndDate = null,
        bool? IsActive = null
    );

    public record TaskItemDto(
        Guid Id,
        Guid UserId,
        string Title,
        string? Description,
        TaskCategory Category,
        string? OtherCategoryName,
        DateTime Date,
        DateTime Time,
        int DurationMinutes,
        bool ReminderEnabled,
        RepeatType RepeatType,
        bool CreatedBySuggestionEngine,
        bool IsApproved,
        Mindflow_Web_API.Models.TaskStatus Status,
        // Recurring task fields
        Guid? ParentTaskId,
        bool IsTemplate,
        DateTime? NextOccurrence,
        int? MaxOccurrences,
        DateTime? EndDate,
        bool IsActive
    );
    public record StatusUpdateDto(Mindflow_Web_API.Models.TaskStatus Status);

}

using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.DTOs
{
    public record CreateTaskItemDto(
        string Title,
        string? Description,
        TaskCategory Category,
        string? OtherCategoryName,
        DateTime Date,
        string Time,
        int DurationMinutes,
        bool ReminderEnabled,
        RepeatType RepeatType,
        Mindflow_Web_API.Models.TaskStatus Status
    );

    public record UpdateTaskItemDto(
        string? Title,
        string? Description,
        TaskCategory? Category,
        string? OtherCategoryName,
        DateTime? Date,
        string? Time,
        int? DurationMinutes,
        bool? ReminderEnabled,
        RepeatType? RepeatType,
        bool? IsApproved,
        Mindflow_Web_API.Models.TaskStatus? Status
    );

    public record TaskItemDto(
        Guid Id,
        Guid UserId,
        string Title,
        string? Description,
        TaskCategory Category,
        string? OtherCategoryName,
        DateTime Date,
        string Time,
        int DurationMinutes,
        bool ReminderEnabled,
        RepeatType RepeatType,
        bool CreatedBySuggestionEngine,
        bool IsApproved,
        Mindflow_Web_API.Models.TaskStatus Status
    );
    public record StatusUpdateDto(Mindflow_Web_API.Models.TaskStatus Status);

}

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
        string? TimeShift,
        int DurationMinutes,
        bool ReminderEnabled,
        RepeatType RepeatType
    );

    public record UpdateTaskItemDto(
        string? Title,
        string? Description,
        TaskCategory? Category,
        string? OtherCategoryName,
        DateTime? Date,
        string? Time,
        string? TimeShift,
        int? DurationMinutes,
        bool? ReminderEnabled,
        RepeatType? RepeatType,
        bool? IsApproved
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
        string? TimeShift,
        int DurationMinutes,
        bool ReminderEnabled,
        RepeatType RepeatType,
        bool CreatedBySuggestionEngine,
        bool IsApproved
    );

}

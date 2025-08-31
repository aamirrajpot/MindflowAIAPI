using System;

namespace Mindflow_Web_API.Models
{
    public class TaskItem : EntityBase
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaskCategory Category { get; set; }
        public string? OtherCategoryName { get; set; } // Only if Category == Other
        public DateTime Date { get; set; }
        public DateTime Time { get; set; } // Time component of the task (stored in UTC)
        public int DurationMinutes { get; set; }
        public bool ReminderEnabled { get; set; }
        public RepeatType RepeatType { get; set; } = RepeatType.Never;
        public bool CreatedBySuggestionEngine { get; set; }
        public bool IsApproved { get; set; }
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
    }

    public enum TaskStatus
    {
        Pending,
        Completed,
        Skipped,
        Cancelled
    }
}

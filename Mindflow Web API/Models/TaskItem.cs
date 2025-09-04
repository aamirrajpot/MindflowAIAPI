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
        
        // Recurring task fields
        public Guid? ParentTaskId { get; set; } // For recurring instances - references the template task
        public bool IsTemplate { get; set; } = false; // True for template tasks, false for instances
        public DateTime? NextOccurrence { get; set; } // When to generate the next instance (for templates)
        public int? MaxOccurrences { get; set; } // Optional limit on number of occurrences
        public DateTime? EndDate { get; set; } // Optional end date for recurring tasks
        public bool IsActive { get; set; } = true; // Whether the recurring series is active
    }

    public enum TaskStatus
    {
        Pending,
        Completed,
        Skipped,
        Cancelled
    }
}

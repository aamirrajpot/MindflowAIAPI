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
        public string Time { get; set; } = string.Empty; // e.g., "09:00"
        public string? TimeShift { get; set; } // "AM" or "PM"
        public int DurationMinutes { get; set; }
        public bool ReminderEnabled { get; set; }
        public RepeatType RepeatType { get; set; } = RepeatType.Never;
        public bool CreatedBySuggestionEngine { get; set; }
        public bool IsApproved { get; set; }
    }
}

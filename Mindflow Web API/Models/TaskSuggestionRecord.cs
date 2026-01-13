using System;
using System.ComponentModel.DataAnnotations;

namespace Mindflow_Web_API.Models
{
    public class TaskSuggestionRecord : EntityBase
    {
        public Guid UserId { get; set; }
        public Guid BrainDumpEntryId { get; set; }

        [MaxLength(500)]
        public string Task { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string Frequency { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Duration { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Priority { get; set; }

        [MaxLength(50)]
        public string? SuggestedTime { get; set; }

        [MaxLength(20)]
        public string? Urgency { get; set; }

        [MaxLength(20)]
        public string? Importance { get; set; }

        public int? PriorityScore { get; set; }

        // Stored as JSON array string
        [MaxLength(2000)]
        public string? SubSteps { get; set; }

        public TaskSuggestionStatus Status { get; set; } = TaskSuggestionStatus.Suggested;

        public Guid? TaskItemId { get; set; } // Links to created TaskItem when scheduled

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum TaskSuggestionStatus
    {
        Suggested = 0,
        Scheduled = 1,
        Skipped = 2
    }
}


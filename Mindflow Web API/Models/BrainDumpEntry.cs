using System;
using System.ComponentModel.DataAnnotations;

namespace Mindflow_Web_API.Models
{
	public enum BrainDumpSource
	{
		Unknown = 0,
		Mobile = 1,
		Web = 2
	}

	public class BrainDumpEntry : EntityBase
	{
		public Guid UserId { get; set; }
		[MaxLength(20000)]
		public string Text { get; set; } = string.Empty;
		[MaxLength(4000)]
		public string? Context { get; set; }
		public int? Mood { get; set; }
		public int? Stress { get; set; }
		public int? Purpose { get; set; }
		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
		public BrainDumpSource Source { get; set; } = BrainDumpSource.Unknown;
		[MaxLength(64)]
		public string? PromptHash { get; set; }
		public int? TokensEstimate { get; set; }
		[MaxLength(1000)]
		public string? SuggestionsPreview { get; set; }
		public bool IsFlagged { get; set; }
		[MaxLength(512)]
		public string? FlagReason { get; set; }
		public DateTime? DeletedAtUtc { get; set; }
		
		// Journal-specific fields
		[MaxLength(200)]
		public string? Title { get; set; } // "Morning Reflections", etc.
		[MaxLength(1000)]
		public string? Tags { get; set; } // Comma-separated tags like "gratitude,morning"
		public bool IsFavorite { get; set; } = false; // For favorites filter
		[MaxLength(1000)]
		public string? AiInsight { get; set; } // AI-generated insights
		public int WordCount { get; set; } = 0; // Word count for statistics
	}
}



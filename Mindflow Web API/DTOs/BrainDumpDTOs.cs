using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.DTOs
{
	public class BrainDumpRequest
	{
		[Required]
		[MinLength(3)]
		public string Text { get; set; } = string.Empty;

		public string? Context { get; set; }

		// Optional sliders from UI (0-10). Nullable to keep payload light
		[Range(0, 10)]
		public int? Mood { get; set; }

		[Range(0, 10)]
		public int? Stress { get; set; }

		[Range(0, 10)]
		public int? Purpose { get; set; }
	}

	public class AddToCalendarRequest
	{
		[Required]
		public string Task { get; set; } = string.Empty;

		[Required]
		public string Frequency { get; set; } = string.Empty;

		[Required]
		public string Duration { get; set; } = string.Empty;

		public string? Notes { get; set; }

		// Optional scheduling details
		public DateTime? Date { get; set; }
		public TimeSpan? Time { get; set; }
		public bool ReminderEnabled { get; set; } = false;
	}

	public class AddMultipleTasksRequest
	{
		[Required]
		public List<TaskSuggestion> Suggestions { get; set; } = new();
	}

	public class BrainDumpResponse
	{
		[JsonPropertyName("userProfile")]
		public UserProfileSummary UserProfile { get; set; } = new();
		[JsonPropertyName("keyThemes")]
		public List<string> KeyThemes { get; set; } = new();
		[JsonPropertyName("aiSummary")]
		public string AiSummary { get; set; } = string.Empty;
		[JsonPropertyName("suggestedActivities")]
		public List<TaskSuggestion> SuggestedActivities { get; set; } = new();
		[JsonPropertyName("weeklyTrends")]
		public WeeklyTrendsData? WeeklyTrends { get; set; }
	}

	public class UserProfileSummary
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;
		[JsonPropertyName("currentState")]
		public string CurrentState { get; set; } = string.Empty; // e.g., "Reflective & Optimistic"
		[JsonPropertyName("emoji")]
		public string Emoji { get; set; } = "😊";
	}

	public class WeeklyTrendsData
	{
		public List<DailyTrend> DailyTrends { get; set; } = new();
		public int CurrentMoodScore { get; set; }
		public int CurrentStressScore { get; set; }
	}

	public class DailyTrend
	{
		public string Day { get; set; } = string.Empty;
		public int MoodScore { get; set; }
		public int StressScore { get; set; }
		public DateTime Date { get; set; }
	}
}



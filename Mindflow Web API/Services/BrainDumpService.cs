using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Utilities;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Mindflow_Web_API.Services
{
	public interface IBrainDumpService
	{
		Task<BrainDumpResponse> GetTaskSuggestionsAsync(Guid userId, BrainDumpRequest request, int maxTokens = 1000, double temperature = 0.7);
		Task<TaskItem> AddTaskToCalendarAsync(Guid userId, AddToCalendarRequest request);
	}

	public class BrainDumpService : IBrainDumpService
	{
		private readonly IRunPodService _runPodService;
		private readonly MindflowDbContext _db;
		private readonly ILogger<BrainDumpService> _logger;
		private readonly IWellnessCheckInService _wellnessService;
		private readonly IUserService _userService;

		public BrainDumpService(IRunPodService runPodService, ILogger<BrainDumpService> logger, MindflowDbContext db, IWellnessCheckInService wellnessService, IUserService userService)
		{
			_runPodService = runPodService;
			_logger = logger;
			_db = db;
			_wellnessService = wellnessService;
			_userService = userService;
		}

		public async Task<BrainDumpResponse> GetTaskSuggestionsAsync(Guid userId, BrainDumpRequest request, int maxTokens = 1200, double temperature = 0.7)
		{
			// Get user's wellness data and profile for personalized prompts
			var wellnessData = await _wellnessService.GetAsync(userId);
			var userProfile = await _userService.GetProfileAsync(userId);
			
			// Get user's display name (FirstName LastName or UserName as fallback)
			var userName = GetUserDisplayName(userProfile);
			
			var prompt = BrainDumpPromptBuilder.BuildTaskSuggestionsPrompt(request, wellnessData, userName);

			// Create entry
			var entry = new BrainDumpEntry
			{
				UserId = userId,
				Text = request.Text,
				Context = request.Context,
				Mood = request.Mood,
				Stress = request.Stress,
				Purpose = request.Purpose,
				TokensEstimate = request.Text?.Length,
				CreatedAtUtc = DateTime.UtcNow
			};
			_db.BrainDumpEntries.Add(entry);
			await _db.SaveChangesAsync();

			var response = await _runPodService.SendPromptAsync(prompt, maxTokens, temperature);

			// Parse enhanced response
			var brainDumpResponse = BrainDumpPromptBuilder.ParseBrainDumpResponse(response, _logger);
			
			if (brainDumpResponse == null)
			{
				throw new InvalidOperationException("Failed to parse AI response");
			}

			// Save preview
			entry.SuggestionsPreview = brainDumpResponse.SuggestedActivities != null && brainDumpResponse.SuggestedActivities.Count > 0
				? string.Join("; ", brainDumpResponse.SuggestedActivities.Take(3).Select(t => t.Task))
				: null;
			await _db.SaveChangesAsync();

			// Add weekly trends data if available
			brainDumpResponse.WeeklyTrends = await GetWeeklyTrendsAsync(userId);

			return brainDumpResponse;
		}

		public async Task<TaskItem> AddTaskToCalendarAsync(Guid userId, AddToCalendarRequest request)
		{
			// Parse duration to minutes
			var durationMinutes = ParseDurationToMinutes(request.Duration);
			if (durationMinutes <= 0)
				throw new ArgumentException("Invalid duration format");

			// Map frequency to RepeatType
			var repeatType = MapFrequencyToRepeatType(request.Frequency);

			// Determine category based on task content
			var category = DetermineTaskCategory(request.Task, request.Notes);

			// Get user's wellness data for available time slots
			var wellnessData = await _wellnessService.GetAsync(userId);
			var (taskDate, taskTime) = DetermineOptimalSchedule(request, wellnessData);

			// Create TaskItem
			var taskItem = new TaskItem
			{
				UserId = userId,
				Title = request.Task,
				Description = request.Notes,
				Category = category,
				OtherCategoryName = category == TaskCategory.Other ? "AI Suggested" : null,
				Date = taskDate,
				Time = taskDate.Date.Add(taskTime), // Combine date and time
				DurationMinutes = durationMinutes,
				ReminderEnabled = request.ReminderEnabled,
				RepeatType = repeatType,
				CreatedBySuggestionEngine = true,
				IsApproved = true, // Auto-approve since user explicitly selected it
				Status = Models.TaskStatus.Pending
			};

			_db.Tasks.Add(taskItem);
			await _db.SaveChangesAsync();

			return taskItem;
		}

		private async Task<WeeklyTrendsData?> GetWeeklyTrendsAsync(Guid userId)
		{
			try
			{
				// Get brain dump entries from the last 7 days
				var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
				var recentEntries = await _db.BrainDumpEntries
					.Where(e => e.UserId == userId && e.CreatedAtUtc >= sevenDaysAgo)
					.OrderBy(e => e.CreatedAtUtc)
					.ToListAsync();

				if (!recentEntries.Any())
					return null;

				var dailyTrends = new List<DailyTrend>();
				var currentDate = sevenDaysAgo.Date;

				// Generate trends for each day in the last 7 days
				for (int i = 0; i < 7; i++)
				{
					var dayDate = currentDate.AddDays(i);
					var dayEntries = recentEntries.Where(e => e.CreatedAtUtc.Date == dayDate).ToList();
					var moodAvg = dayEntries.Any() ? (int)Math.Round(dayEntries.Where(e => e.Mood.HasValue).DefaultIfEmpty().Average(e => (double?)(e?.Mood ?? 0)) ?? 0) : 0;
					var stressAvg = dayEntries.Any() ? (int)Math.Round(dayEntries.Where(e => e.Stress.HasValue).DefaultIfEmpty().Average(e => (double?)(e?.Stress ?? 0)) ?? 0) : 0;

					dailyTrends.Add(new DailyTrend
					{
						Day = dayDate.ToString("ddd"),
						Date = dayDate,
						MoodScore = moodAvg,
						StressScore = stressAvg
					});
				}

				// Get current scores from the most recent entry
				var latest = recentEntries.OrderByDescending(e => e.CreatedAtUtc).First();
				var currentMoodScore = latest.Mood ?? 0;
				var currentStressScore = latest.Stress ?? 0;

				return new WeeklyTrendsData
				{
					DailyTrends = dailyTrends,
					CurrentMoodScore = currentMoodScore,
					CurrentStressScore = currentStressScore
				};
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to get weekly trends for user {UserId}", userId);
				return null;
			}
		}

		private static int ParseDurationToMinutes(string duration)
		{
			if (string.IsNullOrWhiteSpace(duration))
				return 30; // Default 30 minutes

			// Handle ranges like "1-2 hours", "10-15 minutes"
			var rangeMatch = Regex.Match(duration, @"(\d+)-(\d+)\s*(hour|minute|hr|min)s?", RegexOptions.IgnoreCase);
			if (rangeMatch.Success)
			{
				var min = int.Parse(rangeMatch.Groups[1].Value);
				var max = int.Parse(rangeMatch.Groups[2].Value);
				var unit = rangeMatch.Groups[3].Value.ToLower();
				
				var avgMinutes = (min + max) / 2;
				return unit.StartsWith("hour") || unit == "hr" ? avgMinutes * 60 : avgMinutes;
			}

			// Handle single values like "2 hours", "30 minutes"
			var singleMatch = Regex.Match(duration, @"(\d+)\s*(hour|minute|hr|min)s?", RegexOptions.IgnoreCase);
			if (singleMatch.Success)
			{
				var value = int.Parse(singleMatch.Groups[1].Value);
				var unit = singleMatch.Groups[2].Value.ToLower();
				return unit.StartsWith("hour") || unit == "hr" ? value * 60 : value;
			}

			// Handle "Ongoing" or other text
			if (duration.ToLower().Contains("ongoing"))
				return 60; // Default 1 hour for ongoing tasks

			return 30; // Default fallback
		}

		private static RepeatType MapFrequencyToRepeatType(string frequency)
		{
			if (string.IsNullOrWhiteSpace(frequency))
				return RepeatType.Never;

			var freq = frequency.ToLower();
			return freq switch
			{
				"daily" or "every day" => RepeatType.Day,
				"weekly" or "every week" => RepeatType.Week,
				"monthly" or "every month" => RepeatType.Month,
				"weekdays" or "weekday" => RepeatType.Day, // Could be enhanced to handle weekdays specifically
				"bi-weekly" or "biweekly" => RepeatType.Week, // Could be enhanced for every 2 weeks
				"once" or "one time" or "never" => RepeatType.Never,
				_ => RepeatType.Never
			};
		}

		private static TaskCategory DetermineTaskCategory(string task, string? notes)
		{
			var text = $"{task} {notes}".ToLower();
			
			if (text.Contains("breath") || text.Contains("meditation") || text.Contains("mindful"))
				return TaskCategory.Breathing;
			
			if (text.Contains("gratitude") || text.Contains("thankful") || text.Contains("appreciate"))
				return TaskCategory.Gratitude;
			
			if (text.Contains("walk") || text.Contains("exercise") || text.Contains("movement") || text.Contains("run") || text.Contains("yoga"))
				return TaskCategory.Movement;
			
			if (text.Contains("relax") || text.Contains("rest") || text.Contains("calm") || text.Contains("peace"))
				return TaskCategory.Relaxation;
			
			if (text.Contains("journal") || text.Contains("reflect") || text.Contains("think") || text.Contains("review"))
				return TaskCategory.Reflection;
			
			return TaskCategory.Other;
		}

		private static (DateTime date, TimeSpan time) DetermineOptimalSchedule(AddToCalendarRequest request, DTOs.WellnessCheckInDto? wellnessData)
		{
			// If user provided specific date/time, use it
			if (request.Date.HasValue && request.Time.HasValue)
			{
				return (request.Date.Value, request.Time.Value);
			}

			// If user provided only date, use their available time slots
			if (request.Date.HasValue)
			{
				var time = GetOptimalTimeForDate(request.Date.Value, wellnessData);
				return (request.Date.Value, time);
			}

			// If user provided only time, find next available date
			if (request.Time.HasValue)
			{
				var date = GetNextAvailableDate(request.Time.Value, wellnessData);
				return (date, request.Time.Value);
			}

			// No specific preferences - find optimal date and time
			return GetOptimalDateTime(wellnessData);
		}

		private static TimeSpan GetOptimalTimeForDate(DateTime date, DTOs.WellnessCheckInDto? wellnessData)
		{
			if (wellnessData == null)
				return new TimeSpan(9, 0, 0); // Default 9 AM

			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			
			if (isWeekend)
			{
				// Use weekend free time
				var startTime = ParseTimeString(wellnessData.WeekendStartTime, wellnessData.WeekendStartShift);
				var endTime = ParseTimeString(wellnessData.WeekendEndTime, wellnessData.WeekendEndShift);
				return GetOptimalTimeInRange(startTime, endTime);
			}
			else
			{
				// Use weekday free time
				var startTime = ParseTimeString(wellnessData.WeekdayStartTime, wellnessData.WeekdayStartShift);
				var endTime = ParseTimeString(wellnessData.WeekdayEndTime, wellnessData.WeekdayEndShift);
				return GetOptimalTimeInRange(startTime, endTime);
			}
		}

		private static DateTime GetNextAvailableDate(TimeSpan preferredTime, DTOs.WellnessCheckInDto? wellnessData)
		{
			if (wellnessData == null)
				return DateTime.Today.AddDays(1); // Tomorrow

			// Check next 7 days for availability
			for (int i = 1; i <= 7; i++)
			{
				var checkDate = DateTime.Today.AddDays(i);
				var isWeekend = checkDate.DayOfWeek == DayOfWeek.Saturday || checkDate.DayOfWeek == DayOfWeek.Sunday;
				
				TimeSpan startTime, endTime;
				if (isWeekend)
				{
					startTime = ParseTimeString(wellnessData.WeekendStartTime, wellnessData.WeekendStartShift);
					endTime = ParseTimeString(wellnessData.WeekendEndTime, wellnessData.WeekendEndShift);
				}
				else
				{
					startTime = ParseTimeString(wellnessData.WeekdayStartTime, wellnessData.WeekdayStartShift);
					endTime = ParseTimeString(wellnessData.WeekdayEndTime, wellnessData.WeekdayEndShift);
				}

				// Check if preferred time falls within available range
				if (preferredTime >= startTime && preferredTime <= endTime)
				{
					return checkDate;
				}
			}

			// Fallback to tomorrow with optimal time
			return DateTime.Today.AddDays(1);
		}

		private static (DateTime date, TimeSpan time) GetOptimalDateTime(DTOs.WellnessCheckInDto? wellnessData)
		{
			if (wellnessData == null)
				return (DateTime.Today.AddDays(1), new TimeSpan(9, 0, 0));

			// Prefer weekdays for productivity tasks, weekends for relaxation
			var isWeekend = DateTime.Today.DayOfWeek == DayOfWeek.Saturday || DateTime.Today.DayOfWeek == DayOfWeek.Sunday;
			var targetDate = DateTime.Today.AddDays(1); // Start with tomorrow

			// If today is weekend, prefer next weekday
			if (isWeekend)
			{
				targetDate = DateTime.Today.AddDays(1);
				while (targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday)
				{
					targetDate = targetDate.AddDays(1);
				}
			}

			var time = GetOptimalTimeForDate(targetDate, wellnessData);
			return (targetDate, time);
		}

		private static TimeSpan ParseTimeString(string? timeStr, string? shift)
		{
			if (string.IsNullOrWhiteSpace(timeStr))
				return new TimeSpan(9, 0, 0); // Default 9 AM

			// Parse time like "09:30" or "9:30"
			if (TimeSpan.TryParse(timeStr, out var time))
			{
				// Handle AM/PM shift
				if (!string.IsNullOrWhiteSpace(shift))
				{
					var isPM = shift.ToUpper().Contains("PM");
					var isAM = shift.ToUpper().Contains("AM");
					
					if (isPM && time.Hours < 12)
					{
						time = time.Add(new TimeSpan(12, 0, 0)); // Add 12 hours for PM
					}
					else if (isAM && time.Hours == 12)
					{
						time = time.Subtract(new TimeSpan(12, 0, 0)); // Subtract 12 hours for 12 AM
					}
				}
				
				return time;
			}

			return new TimeSpan(9, 0, 0); // Default fallback
		}

		private static TimeSpan GetOptimalTimeInRange(TimeSpan startTime, TimeSpan endTime)
		{
			// Find a good time within the range (prefer morning hours)
			var range = endTime - startTime;
			var optimalOffset = TimeSpan.FromMinutes(range.TotalMinutes * 0.2); // 20% into the range
			var optimalTime = startTime.Add(optimalOffset);

			// Ensure it's within bounds
			if (optimalTime < startTime) optimalTime = startTime;
			if (optimalTime > endTime) optimalTime = endTime;

			return optimalTime;
		}

		private static string GetUserDisplayName(UserProfileDto? userProfile)
		{
			if (userProfile == null)
				return "User";

			// Try to use FirstName LastName if both are available
			if (!string.IsNullOrWhiteSpace(userProfile.FirstName) && !string.IsNullOrWhiteSpace(userProfile.LastName))
			{
				return $"{userProfile.FirstName.Trim()} {userProfile.LastName.Trim()}";
			}

			// Fallback to FirstName only
			if (!string.IsNullOrWhiteSpace(userProfile.FirstName))
			{
				return userProfile.FirstName.Trim();
			}

			// Fallback to UserName
			if (!string.IsNullOrWhiteSpace(userProfile.UserName))
			{
				return userProfile.UserName.Trim();
			}

			// Final fallback
			return "User";
		}
	}
}



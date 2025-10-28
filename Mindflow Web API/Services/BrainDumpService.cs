using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
		Task<List<TaskItem>> AddMultipleTasksToCalendarAsync(Guid userId, List<TaskSuggestion> suggestions, DTOs.WellnessCheckInDto? wellnessData = null);
		Task<string?> GenerateAiInsightAsync(Guid userId, Guid entryId);
	}

	public class BrainDumpService : IBrainDumpService
	{
		private readonly IRunPodService _runPodService;
		private readonly MindflowDbContext _db;
		private readonly ILogger<BrainDumpService> _logger;
		private readonly IWellnessCheckInService _wellnessService;
		private readonly IUserService _userService;
		private readonly IServiceProvider _serviceProvider;

		public BrainDumpService(IRunPodService runPodService, ILogger<BrainDumpService> logger, MindflowDbContext db, IWellnessCheckInService wellnessService, IUserService userService, IServiceProvider serviceProvider)
		{
			_runPodService = runPodService;
			_logger = logger;
			_db = db;
			_wellnessService = wellnessService;
			_userService = userService;
			_serviceProvider = serviceProvider;
		}

		public async Task<BrainDumpResponse> GetTaskSuggestionsAsync(Guid userId, BrainDumpRequest request, int maxTokens = 1200, double temperature = 0.7)
		{
			// Get user's wellness data and profile for personalized prompts
			var wellnessData = await _wellnessService.GetAsync(userId);
			var userProfile = await _userService.GetProfileAsync(userId);
			
			// Get user's display name (FirstName LastName or UserName as fallback)
			var userName = GetUserDisplayName(userProfile);
			
			// Extract tags using LLM
			var extractedTags = await ExtractTagsFromTextAsync(request.Text ?? string.Empty);
			
			var prompt = BrainDumpPromptBuilder.BuildTaskSuggestionsPrompt(request, wellnessData, userName);

			// Create entry
			var entry = new BrainDumpEntry
			{
				UserId = userId,
				Text = request.Text ?? string.Empty,
				Context = request.Context,
				Mood = request.Mood,
				Stress = request.Stress,
				Purpose = request.Purpose,
				TokensEstimate = request.Text?.Length,
				CreatedAtUtc = DateTime.UtcNow,
				// Journal-specific fields
				Title = GenerateDefaultTitle(request.Text ?? string.Empty),
				WordCount = CalculateWordCount(request.Text ?? string.Empty),
				Tags = extractedTags,
				IsFavorite = false,
				Source = BrainDumpSource.Web // Brain dump comes from web/mobile app
			};
			_db.BrainDumpEntries.Add(entry);
			await _db.SaveChangesAsync();

			BrainDumpResponse? brainDumpResponse = null;
			string response = string.Empty;
			int attempt = 0;
			int maxAttempts = 3; // initial + 2 retries
			double currentTemperature = temperature;
			bool forceMinimumActivities = false;
			while (attempt < maxAttempts)
			{
				response = await _runPodService.SendPromptAsync(prompt, maxTokens, currentTemperature);
				brainDumpResponse = BrainDumpPromptBuilder.ParseBrainDumpResponse(response, _logger);
				
				var hasActivities = brainDumpResponse != null && brainDumpResponse.SuggestedActivities != null && brainDumpResponse.SuggestedActivities.Count > 0;
				if (brainDumpResponse != null && hasActivities)
				{
					break; // success
				}

				attempt++;
				_logger.LogWarning("BrainDump suggestions empty (attempt {Attempt}/{Max}). Retrying with stricter prompt.", attempt, maxAttempts);
				// Tighten prompt and reduce temperature for determinism
				forceMinimumActivities = true;
				currentTemperature = Math.Max(0.2, currentTemperature - 0.2);
				prompt = BrainDumpPromptBuilder.BuildTaskSuggestionsPrompt(request, wellnessData, userName, forceMinimumActivities);
			}

			if (brainDumpResponse == null)
			{
				throw new InvalidOperationException("Failed to parse AI response");
			}

			// If still no activities after retries, synthesize a minimal fallback list
			if (brainDumpResponse.SuggestedActivities == null || brainDumpResponse.SuggestedActivities.Count == 0)
			{
				_logger.LogWarning("AI returned no SuggestedActivities after retries. Generating fallback suggestions.");
				brainDumpResponse.SuggestedActivities = GenerateFallbackActivities(request);
			}

			// Save preview
			entry.SuggestionsPreview = brainDumpResponse.SuggestedActivities != null && brainDumpResponse.SuggestedActivities.Count > 0
				? string.Join("; ", brainDumpResponse.SuggestedActivities.Take(3).Select(t => t.Task))
				: null;
			await _db.SaveChangesAsync();

			// Generate AI insight asynchronously (don't await to avoid blocking)
			_ = Task.Run(async () =>
			{
				try
				{
					// Create a new scope for the background task to avoid disposed context issues
					using var scope = _serviceProvider.CreateScope();
					var dbContext = scope.ServiceProvider.GetRequiredService<MindflowDbContext>();
					var runPodService = scope.ServiceProvider.GetRequiredService<IRunPodService>();
					var logger = scope.ServiceProvider.GetRequiredService<ILogger<BrainDumpService>>();
					
					// Create a temporary service instance for the background task
					var tempService = new BrainDumpService(runPodService, logger, dbContext, _wellnessService, _userService, _serviceProvider);
					
					var insight = await tempService.GenerateAiInsightAsync(userId, entry.Id);
					if (!string.IsNullOrEmpty(insight))
					{
						// Update the entry in the new context
						var entryToUpdate = await dbContext.BrainDumpEntries.FindAsync(entry.Id);
						if (entryToUpdate != null)
						{
							entryToUpdate.AiInsight = insight;
							await dbContext.SaveChangesAsync();
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to generate AI insight for brain dump entry {EntryId}", entry.Id);
				}
			});

			// Add weekly trends data if available
			brainDumpResponse.WeeklyTrends = await GetWeeklyTrendsAsync(userId);

			return brainDumpResponse;
		}

		private static List<TaskSuggestion> GenerateFallbackActivities(BrainDumpRequest request)
		{
			var text = (request.Text ?? string.Empty).ToLower();
			var list = new List<TaskSuggestion>();
			// Basic heuristics to generate 4 concise tasks
			if (text.Contains("anxiety") || text.Contains("stress") || text.Contains("overwhelm"))
			{
				list.Add(new TaskSuggestion { Task = "Do a 5-minute deep breathing session", Frequency = "Once today", Duration = "5 minutes", Notes = "Helps lower cortisol and reset focus", Priority = "High", SuggestedTime = "Afternoon" });
			}
			if (text.Contains("sleep"))
			{
				list.Add(new TaskSuggestion { Task = "Prepare a simple wind-down routine", Frequency = "Tonight", Duration = "10 minutes", Notes = "Light stretch, no screens for 30 minutes", Priority = "Medium", SuggestedTime = "Evening" });
			}
			if (text.Contains("email") || text.Contains("inbox") || text.Contains("admin"))
			{
				list.Add(new TaskSuggestion { Task = "Clear your top 5 emails", Frequency = "Once today", Duration = "15 minutes", Notes = "Reply or archive; schedule longer replies", Priority = "Medium", SuggestedTime = "Morning" });
			}
			if (text.Contains("call") || text.Contains("doctor") || text.Contains("appointment"))
			{
				list.Add(new TaskSuggestion { Task = "Schedule the pending appointment", Frequency = "Once today", Duration = "10 minutes", Notes = "Pick a date within 2 weeks", Priority = "High", SuggestedTime = "Morning" });
			}
			// Always include two general-purpose wellness tasks if list is short
			if (list.Count < 4)
			{
				list.Add(new TaskSuggestion { Task = "Take a 10-minute walk", Frequency = "Once today", Duration = "10 minutes", Notes = "Gentle movement boosts mood and clarity", Priority = "Medium", SuggestedTime = "Afternoon" });
			}
			if (list.Count < 4)
			{
				list.Add(new TaskSuggestion { Task = "Write 3 lines of reflection", Frequency = "Once today", Duration = "5 minutes", Notes = "Capture one worry and one win", Priority = "Low", SuggestedTime = "Evening" });
			}
			return list.Take(6).ToList();
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
			
		// Use the new smart scheduling logic
			var (taskDate, taskTime) = await DetermineOptimalScheduleWithTimeSlotsAsync(request, wellnessData, durationMinutes, userId);

			// Compose UTC datetime for storage/return
			// Note: taskTime is already in UTC format from the scheduling logic
			var utcDateTime = DateTime.SpecifyKind(taskDate.Date.Add(taskTime), DateTimeKind.Utc);

			_logger.LogInformation("Creating task '{TaskTitle}' for user {UserId} at {TaskDate} {TaskTime} UTC", 
				request.Task, userId, taskDate.ToString("yyyy-MM-dd"), taskTime);

			// Create TaskItem
			var taskItem = new TaskItem
			{
				UserId = userId,
				Title = request.Task,
				Description = request.Notes,
				Category = category,
				OtherCategoryName = category == TaskCategory.Other ? "AI Suggested" : null,
				Date = utcDateTime.Date,
				Time = utcDateTime, // UTC
				DurationMinutes = durationMinutes,
				ReminderEnabled = request.ReminderEnabled,
				RepeatType = repeatType,
				CreatedBySuggestionEngine = true,
				IsApproved = true, // Auto-approve since user explicitly selected it
				Status = Models.TaskStatus.Pending,
				// Recurring task fields
				IsTemplate = repeatType != RepeatType.Never, // Create template for recurring tasks
				NextOccurrence = repeatType != RepeatType.Never ? CalculateNextOccurrence(utcDateTime, repeatType) : null,
				IsActive = true
			};

			_db.Tasks.Add(taskItem);
			await _db.SaveChangesAsync();

			return taskItem;
		}

		public async Task<List<TaskItem>> AddMultipleTasksToCalendarAsync(Guid userId, List<TaskSuggestion> suggestions, DTOs.WellnessCheckInDto? wellnessData = null)
		{
			var createdTasks = new List<TaskItem>();
			
			// Get user's wellness data if not provided
			if (wellnessData == null)
				wellnessData = await _wellnessService.GetAsync(userId);

			// Sort tasks by priority (High -> Medium -> Low)
			var sortedSuggestions = suggestions.OrderBy(s => s.Priority switch
			{
				"High" => 1,
				"Medium" => 2,
				"Low" => 3,
				_ => 2
			}).ToList();

			// Schedule tasks across available time slots
			var scheduledTasks = ScheduleTasksAcrossTimeSlots(sortedSuggestions, wellnessData);

			foreach (var scheduledTask in scheduledTasks)
			{
				var taskItem = new TaskItem
				{
					UserId = userId,
					Title = scheduledTask.Suggestion.Task,
					Description = scheduledTask.Suggestion.Notes,
					Category = DetermineTaskCategory(scheduledTask.Suggestion.Task, scheduledTask.Suggestion.Notes),
					OtherCategoryName = "AI Suggested",
					Date = DateTime.SpecifyKind(scheduledTask.Date.Date.Add(scheduledTask.Time), DateTimeKind.Utc).Date,
					Time = DateTime.SpecifyKind(scheduledTask.Date.Date.Add(scheduledTask.Time), DateTimeKind.Utc),
					DurationMinutes = ParseDurationToMinutes(scheduledTask.Suggestion.Duration),
					ReminderEnabled = true,
					RepeatType = MapFrequencyToRepeatType(scheduledTask.Suggestion.Frequency),
					CreatedBySuggestionEngine = true,
					IsApproved = true,
					Status = Models.TaskStatus.Pending,
					IsTemplate = false,
					IsActive = true
				};

				_db.Tasks.Add(taskItem);
				createdTasks.Add(taskItem);
			}

			await _db.SaveChangesAsync();
			return createdTasks;
		}

	private List<ScheduledTask> ScheduleTasksAcrossTimeSlots(List<TaskSuggestion> suggestions, DTOs.WellnessCheckInDto? wellnessData)
	{
		var scheduledTasks = new List<ScheduledTask>();
		
		_logger.LogInformation("Starting task scheduling for {TaskCount} tasks", suggestions.Count);
		
		// Parse available time slots
		var weekdaySlots = ParseTimeSlots(wellnessData?.WeekdayStartTime, wellnessData?.WeekdayEndTime, wellnessData?.WeekdayStartShift, wellnessData?.WeekdayEndShift);
		var weekendSlots = ParseTimeSlots(wellnessData?.WeekendStartTime, wellnessData?.WeekendEndTime, wellnessData?.WeekendStartShift, wellnessData?.WeekendEndShift);

		_logger.LogInformation("Weekday slots: {WeekdayStart} to {WeekdayEnd}", weekdaySlots.start, weekdaySlots.end);
		_logger.LogInformation("Weekend slots: {WeekendStart} to {WeekendEnd}", weekendSlots.start, weekendSlots.end);

		// Create a time slot manager to track available slots
		var slotManager = new TimeSlotManager(weekdaySlots, weekendSlots);
		
		// Sort tasks by priority and duration for optimal scheduling
		var sortedSuggestions = suggestions.OrderBy(s => s.Priority switch
		{
			"High" => 1,
			"Medium" => 2,
			"Low" => 3,
			_ => 2
		}).ThenBy(s => ParseDurationToMinutes(s.Duration)).ToList();

		_logger.LogInformation("Task scheduling order: {TaskOrder}", 
			string.Join(", ", sortedSuggestions.Select(s => $"{s.Task} ({s.Priority})")));

		foreach (var suggestion in sortedSuggestions)
		{
			var duration = ParseDurationToMinutes(suggestion.Duration);
			
			_logger.LogDebug("Scheduling task: {Task} (Duration: {Duration}min, Priority: {Priority}, SuggestedTime: {SuggestedTime})", 
				suggestion.Task, duration, suggestion.Priority, suggestion.SuggestedTime ?? "None");
			
			// Find the best available slot for this task
			var (date, time) = FindBestAvailableSlot(suggestion, duration, slotManager);
			
			_logger.LogDebug("Scheduled {Task} for {Date} at {Time}", suggestion.Task, date.ToString("yyyy-MM-dd"), time);
			
			scheduledTasks.Add(new ScheduledTask
			{
				Suggestion = suggestion,
				Date = date,
				Time = time
			});

			// Reserve this time slot
			slotManager.ReserveSlot(date, time, duration);
		}

		_logger.LogInformation("Completed scheduling {ScheduledCount} tasks", scheduledTasks.Count);
		return scheduledTasks;
	}

		private async Task<(DateTime date, TimeSpan time)> DetermineOptimalScheduleWithTimeSlotsAsync(AddToCalendarRequest request, DTOs.WellnessCheckInDto? wellnessData, int durationMinutes, Guid userId)
		{
			// Parse available time slots from wellness data
			var weekdaySlots = ParseTimeSlots(wellnessData?.WeekdayStartTime, wellnessData?.WeekdayEndTime, wellnessData?.WeekdayStartShift, wellnessData?.WeekdayEndShift);
			var weekendSlots = ParseTimeSlots(wellnessData?.WeekendStartTime, wellnessData?.WeekendEndTime, wellnessData?.WeekendStartShift, wellnessData?.WeekendEndShift);

			_logger.LogDebug("Determining optimal schedule for task with duration {Duration} minutes", durationMinutes);
			_logger.LogDebug("Weekday slots: {WeekdayStart} to {WeekdayEnd}", weekdaySlots.start, weekdaySlots.end);
			_logger.LogDebug("Weekend slots: {WeekendStart} to {WeekendEnd}", weekendSlots.start, weekendSlots.end);

			// If user provided specific date and time, try to use it
			if (request.Date.HasValue && request.Time.HasValue)
			{
				var userDate = request.Date.Value;
				var userTime = request.Time.Value;
				
				_logger.LogDebug("User provided specific date and time: {Date} at {Time}", userDate.ToString("yyyy-MM-dd"), userTime);
				
				// Check if the user's preferred time fits within available slots
				var isWeekend = userDate.DayOfWeek == DayOfWeek.Saturday || userDate.DayOfWeek == DayOfWeek.Sunday;
				var slots = isWeekend ? weekendSlots : weekdaySlots;
				
				if (userTime >= slots.start && userTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end)
				{
					// Check if this time slot is available
					if (await IsTimeSlotAvailableAsync(userDate, userTime, durationMinutes, userId))
					{
						_logger.LogDebug("User's preferred time slot is available");
						return (userDate, userTime);
					}
					else
					{
						_logger.LogDebug("User's preferred time slot is not available, finding alternative");
					}
				}
				else
				{
					_logger.LogDebug("User's preferred time is outside available slots");
				}
			}

			// If user provided only date, find best time on that date
			if (request.Date.HasValue)
			{
				var userDate = request.Date.Value;
				var isWeekend = userDate.DayOfWeek == DayOfWeek.Saturday || userDate.DayOfWeek == DayOfWeek.Sunday;
				var slots = isWeekend ? weekendSlots : weekdaySlots;
				
				_logger.LogDebug("User provided specific date: {Date}, finding best time", userDate.ToString("yyyy-MM-dd"));
				
				var availableTime = await FindAvailableTimeInDayAsync(userDate, slots, durationMinutes, userId);
				if (availableTime != TimeSpan.Zero)
				{
					_logger.LogDebug("Found available time on user's preferred date: {Time}", availableTime);
					return (userDate, availableTime);
				}
				else
				{
					_logger.LogDebug("No available time found on user's preferred date");
				}
			}

			// If user provided only time, find next available date
			if (request.Time.HasValue)
			{
				var userTime = request.Time.Value;
				_logger.LogDebug("User provided specific time: {Time}, finding next available date", userTime);
				
				var (date, time) = await FindNextAvailableDateForTimeAsync(userTime, durationMinutes, weekdaySlots, weekendSlots, userId);
				if (date != DateTime.MinValue)
				{
					_logger.LogDebug("Found next available date for user's preferred time: {Date}", date.ToString("yyyy-MM-dd"));
					return (date, time);
				}
			}

			// No specific preferences - find optimal date and time
			_logger.LogDebug("No user preferences, finding optimal date and time");
			var (optimalDate, optimalTime) = await FindNextAvailableSlotAsync(durationMinutes, weekdaySlots, weekendSlots, userId);
			_logger.LogDebug("Found optimal slot: {Date} at {Time}", optimalDate.ToString("yyyy-MM-dd"), optimalTime);
			return (optimalDate, optimalTime);
		}

		private (DateTime date, TimeSpan time) FindBestAvailableSlot(TaskSuggestion suggestion, int durationMinutes, TimeSlotManager slotManager)
		{
			// Try to use AI-suggested time first if available
			if (!string.IsNullOrEmpty(suggestion.SuggestedTime))
			{
				var aiTime = ParseAISuggestedTime(suggestion.SuggestedTime);
				if (aiTime.HasValue)
				{
					// Try to find a slot that matches the AI suggestion
					var (date, time) = slotManager.FindSlotMatchingTime(aiTime.Value, durationMinutes);
					if (date != DateTime.MinValue)
					{
						return (date, time);
					}
				}
			}

			// Fallback to finding the next best available slot
			return slotManager.FindNextAvailableSlot(durationMinutes);
		}

		private async Task<bool> IsTimeSlotAvailableAsync(DateTime date, TimeSpan time, int durationMinutes, Guid userId)
	{
		try
		{
				// Treat input as UTC for comparisons
				var newTaskStart = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Utc);
			var newTaskEnd = newTaskStart.AddMinutes(durationMinutes);
			
			_logger.LogDebug("Checking availability for new task: {StartTime} to {EndTime}", newTaskStart, newTaskEnd);
			
			// Check for existing tasks that overlap with this time slot
				var conflictingTasks = await _db.Tasks
				.Where(t => t.UserId == userId 
					&& t.IsActive 
						&& t.Date.Date == newTaskStart.Date)
				.ToListAsync();
			
			foreach (var existingTask in conflictingTasks)
			{
				var existingTaskStart = existingTask.Time;
				var existingTaskEnd = existingTaskStart.AddMinutes(existingTask.DurationMinutes);
				
				_logger.LogDebug("Checking against existing task: {StartTime} to {EndTime}", existingTaskStart, existingTaskEnd);
				
				// Check for overlap with 15-minute buffer
				var bufferMinutes = 15;
				var newTaskStartWithBuffer = newTaskStart.AddMinutes(-bufferMinutes);
				var newTaskEndWithBuffer = newTaskEnd.AddMinutes(bufferMinutes);
				var existingTaskStartWithBuffer = existingTaskStart.AddMinutes(-bufferMinutes);
				var existingTaskEndWithBuffer = existingTaskEnd.AddMinutes(bufferMinutes);
				
				// Two tasks conflict if their buffered time ranges overlap
				bool hasOverlap = (newTaskStartWithBuffer < existingTaskEndWithBuffer) && (newTaskEndWithBuffer > existingTaskStartWithBuffer);
				
				if (hasOverlap)
				{
					_logger.LogDebug("CONFLICT DETECTED: New task {NewStart}-{NewEnd} overlaps with existing task {ExistingStart}-{ExistingEnd}", 
						newTaskStartWithBuffer, newTaskEndWithBuffer, existingTaskStartWithBuffer, existingTaskEndWithBuffer);
					return false;
				}
			}
			
			_logger.LogDebug("No conflicts found for time slot {Time}", time);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error checking time slot availability for {Date} at {Time}", date, time);
			return false; // Conservative approach - assume slot is not available if we can't check
		}
	}

	private async Task<TimeSpan> FindAvailableTimeInDayAsync(DateTime date, (TimeSpan start, TimeSpan end) slots, int durationMinutes, Guid userId)
	{
		var currentTime = slots.start;
		var endTime = slots.end;

		while (currentTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= endTime)
		{
			if (await IsTimeSlotAvailableAsync(date, currentTime, durationMinutes, userId))
			{
				return currentTime;
			}

			// Move to next 30-minute slot
			currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
		}

		return TimeSpan.Zero;
	}

	private async Task<(DateTime date, TimeSpan time)> FindNextAvailableDateForTimeAsync(TimeSpan preferredTime, int durationMinutes, (TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots, Guid userId)
	{
		var startDate = DateTime.UtcNow.Date.AddDays(1);
		var maxDays = 14;

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = startDate.AddDays(dayOffset);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? weekendSlots : weekdaySlots;

			// Check if preferred time fits within available slots
			if (preferredTime >= slots.start && preferredTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end)
			{
				// Check if this time slot is available
				if (await IsTimeSlotAvailableAsync(date, preferredTime, durationMinutes, userId))
				{
					return (date, preferredTime);
				}
			}
		}

		return (DateTime.MinValue, TimeSpan.Zero);
	}

	private async Task<(DateTime date, TimeSpan time)> FindNextAvailableSlotAsync(int durationMinutes, (TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots, Guid userId)
	{
		var startDate = DateTime.UtcNow.Date.AddDays(1); // Start from tomorrow (UTC)
		var maxDays = 14; // Look ahead 2 weeks

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = startDate.AddDays(dayOffset);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? weekendSlots : weekdaySlots;

			// Find available time within the day's slots
			var availableTime = await FindAvailableTimeInDayAsync(date, slots, durationMinutes, userId);
			if (availableTime != TimeSpan.Zero)
			{
				return (date, availableTime);
			}
		}

		// Fallback: return tomorrow at start of available slots
		var fallbackDate = DateTime.UtcNow.Date.AddDays(1);
		var fallbackIsWeekend = fallbackDate.DayOfWeek == DayOfWeek.Saturday || fallbackDate.DayOfWeek == DayOfWeek.Sunday;
		var fallbackSlots = fallbackIsWeekend ? weekendSlots : weekdaySlots;
		return (fallbackDate, fallbackSlots.start);
	}

		private (DateTime date, TimeSpan time) TryUseAISuggestedTime(TaskSuggestion suggestion, DateTime currentDate, TimeSpan currentTime, int durationMinutes, (TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots)
		{
			// If AI provided a specific time suggestion, try to use it
			if (!string.IsNullOrEmpty(suggestion.SuggestedTime))
			{
				var aiTime = ParseAISuggestedTime(suggestion.SuggestedTime);
				if (aiTime.HasValue)
				{
					// Check if AI-suggested time fits within available slots
					var isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;
					var slots = isWeekend ? weekendSlots : weekdaySlots;
					
					if (aiTime.Value >= slots.start && aiTime.Value.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end)
					{
						return (currentDate, aiTime.Value);
					}
				}
			}

			// Fallback to system-calculated optimal time
			return FindNextAvailableSlot(currentDate, currentTime, durationMinutes, weekdaySlots, weekendSlots);
		}

	private TimeSpan? ParseAISuggestedTime(string suggestedTime)
	{
		// Parse AI-suggested time strings like "Morning", "Afternoon", "Evening", "9:00 AM", etc.
		suggestedTime = suggestedTime.Trim();
		
		// Handle specific times like "9:00 AM", "2:30 PM" - FIXED PARSING
		if (suggestedTime.Contains("AM") || suggestedTime.Contains("PM"))
		{
			// Extract time and AM/PM parts
			var timePart = suggestedTime.Replace("AM", "").Replace("PM", "").Trim();
			var isPM = suggestedTime.ToUpper().Contains("PM");
			
			if (TimeSpan.TryParse(timePart, out var time))
			{
				if (isPM && time.Hours >= 1 && time.Hours <= 12)
				{
					time = time.Add(new TimeSpan(12, 0, 0));
				}
				else if (!isPM && time.Hours == 12)
				{
					time = time.Subtract(new TimeSpan(12, 0, 0));
				}
				return time;
			}
		}
		
		// Handle 24-hour format times like "09:00", "17:00"
		if (TimeSpan.TryParse(suggestedTime, out var time24))
		{
			return time24;
		}
		
		// Handle time periods
		var timeLower = suggestedTime.ToLower();
		return timeLower switch
		{
			"morning" => new TimeSpan(9, 0, 0),   // 9:00 AM
			"afternoon" => new TimeSpan(14, 0, 0), // 2:00 PM
			"evening" => new TimeSpan(18, 0, 0),   // 6:00 PM
			"weekend" => new TimeSpan(10, 0, 0),   // 10:00 AM (weekend start)
			"weekday" => new TimeSpan(9, 0, 0),    // 9:00 AM (weekday start)
			_ => null // Unknown format, let system decide
		};
	}

	private (TimeSpan start, TimeSpan end) ParseTimeSlots(string? startTime, string? endTime, string? startShift, string? endShift)
	{
		if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
		{
			_logger.LogWarning("Missing time data, using default slots: 9 AM to 5 PM");
			return (new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)); // Default 9 AM to 5 PM
		}

		_logger.LogDebug("Parsing time slots - Start: {StartTime} {StartShift}, End: {EndTime} {EndShift}", 
			startTime, startShift, endTime, endShift);

		var start = ParseTimeString(startTime, startShift);
		var end = ParseTimeString(endTime, endShift);
		
		_logger.LogDebug("Parsed time slots - Start: {StartTime}, End: {EndTime}", start, end);
		
		// Validate that start is before end
		if (start >= end)
		{
			_logger.LogWarning("Invalid time slots: start {StartTime} is not before end {EndTime}, using defaults", start, end);
			return (new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));
		}
		
		_logger.LogDebug("Final parsed time slots: {StartTime} to {EndTime}", start, end);
		return (start, end);
	}


		private (DateTime date, TimeSpan time) FindNextAvailableSlot(DateTime currentDate, TimeSpan currentTime, int durationMinutes, (TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots)
		{
			var date = currentDate;
			var time = currentTime;
			var maxAttempts = 14; // Try for 2 weeks
			var attempt = 0;

			while (attempt < maxAttempts)
			{
				var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
				var slots = isWeekend ? weekendSlots : weekdaySlots;

				// Check if current time is within available slots
				if (time >= slots.start && time.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end)
				{
					return (date, time);
				}

				// Move to next available time
				if (time < slots.start)
				{
					time = slots.start;
				}
				else if (time.Add(TimeSpan.FromMinutes(durationMinutes)) > slots.end)
				{
					// Move to next day
					date = date.AddDays(1);
					time = slots.start;
				}
				else
				{
					// Move to next time slot
					time = time.Add(TimeSpan.FromMinutes(30)); // 30-minute increments
				}

				attempt++;
			}

			// Fallback: return current date/time if no slot found
			return (currentDate, currentTime);
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
				return DateTime.UtcNow.Date.AddDays(1); // Tomorrow (UTC)

			// Check next 7 days for availability
			for (int i = 1; i <= 7; i++)
			{
				var checkDate = DateTime.UtcNow.Date.AddDays(i);
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
			return DateTime.UtcNow.Date.AddDays(1);
		}

		private static (DateTime date, TimeSpan time) GetOptimalDateTime(DTOs.WellnessCheckInDto? wellnessData)
		{
			if (wellnessData == null)
				return (DateTime.UtcNow.Date.AddDays(1), new TimeSpan(9, 0, 0));

			// Prefer weekdays for productivity tasks, weekends for relaxation
			var isWeekend = DateTime.UtcNow.DayOfWeek == DayOfWeek.Saturday || DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday;
			var targetDate = DateTime.UtcNow.Date.AddDays(1); // Start with tomorrow (UTC)

			// If today is weekend, prefer next weekday
			if (isWeekend)
			{
				targetDate = DateTime.UtcNow.Date.AddDays(1);
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
			// Handle AM/PM shift - FIXED LOGIC
			if (!string.IsNullOrWhiteSpace(shift))
			{
				var shiftUpper = shift.ToUpper();
				var isPM = shiftUpper.Contains("PM");
				var isAM = shiftUpper.Contains("AM");
				
				if (isPM)
				{
					// For PM times, add 12 hours if it's not already in 24-hour format
					// But only if the time is in 12-hour format (1-12)
					if (time.Hours >= 1 && time.Hours <= 12)
					{
						time = time.Add(new TimeSpan(12, 0, 0));
					}
					// If it's already in 24-hour format (13-23), don't modify
				}
				else if (isAM)
				{
					// For AM times, if it's 12:xx AM, convert to 00:xx
					if (time.Hours == 12)
					{
						time = time.Subtract(new TimeSpan(12, 0, 0));
					}
					// If it's 1-11 AM, it's already correct in 24-hour format
				}
			}
			
			return time;
		}

		return new TimeSpan(9, 0, 0); // Default fallback
	}

		/// <summary>
		/// Converts user's local time slot to UTC time for storage.
		/// This is critical for proper timezone handling.
		/// </summary>
		private static TimeSpan ConvertLocalTimeSlotToUtc(TimeSpan localTime, int? timeZoneOffsetMinutes = null)
		{
			// If no timezone offset provided, assume the time is already in UTC
			if (!timeZoneOffsetMinutes.HasValue)
			{
				return localTime;
			}

			// Convert local time to UTC by subtracting the timezone offset
			// Example: 3PM local (UTC+5) = 10AM UTC
			var offset = TimeSpan.FromMinutes(timeZoneOffsetMinutes.Value);
			var utcTime = localTime.Subtract(offset);
			
			// Handle day boundary crossing
			if (utcTime.TotalMinutes < 0)
			{
				utcTime = utcTime.Add(TimeSpan.FromDays(1));
			}
			else if (utcTime.TotalMinutes >= 1440) // 24 hours
			{
				utcTime = utcTime.Subtract(TimeSpan.FromDays(1));
			}
			
			return utcTime;
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

		private static DateTime CalculateNextOccurrence(DateTime currentDate, RepeatType repeatType)
		{
			return repeatType switch
			{
				RepeatType.Day => currentDate.AddDays(1),
				RepeatType.Week => currentDate.AddDays(7),
				RepeatType.Month => currentDate.AddMonths(1),
				_ => currentDate
			};
		}

		private static string GenerateDefaultTitle(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return "Brain Dump Entry";
			
			// Take first 50 characters and clean up
			var title = text.Substring(0, Math.Min(50, text.Length)).Trim();
			if (title.Length < text.Length) title += "...";
			
			return title;
		}

		private static int CalculateWordCount(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return 0;
			
			// Simple word count - split by whitespace and count non-empty parts
			var words = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			return words.Length;
		}

		private async Task<string> ExtractTagsFromTextAsync(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;

			try
			{
				// Use LLM for intelligent tag extraction
				var prompt = BrainDumpPromptBuilder.BuildTagExtractionPrompt(text);
				var response = await _runPodService.SendPromptAsync(prompt, 200, 0.3); // Lower temperature for more consistent results
				
				var extractedTags = BrainDumpPromptBuilder.ParseTagExtractionResponse(response, _logger);
				
				// Fallback to simple keyword extraction if LLM fails
				if (string.IsNullOrWhiteSpace(extractedTags))
				{
					return ExtractTagsFromTextFallback(text);
				}
				
				return extractedTags;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to extract tags using LLM, falling back to keyword extraction");
				return ExtractTagsFromTextFallback(text);
			}
		}

		private static string ExtractTagsFromTextFallback(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;

			// Simple tag extraction - look for common emotional/wellness keywords
			var commonTags = new[] { "gratitude", "anxiety", "stress", "happy", "sad", "tired", "energized", 
								   "overwhelmed", "peaceful", "frustrated", "excited", "worried", "calm", 
								   "morning", "evening", "work", "family", "health", "exercise", "meditation" };

			var textLower = text.ToLower();
			var foundTags = commonTags.Where(tag => textLower.Contains(tag)).ToList();

			return string.Join(",", foundTags);
		}

		public async Task<string?> GenerateAiInsightAsync(Guid userId, Guid entryId)
		{
			try
			{
				var entry = await _db.BrainDumpEntries
					.FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId);
				
				if (entry == null) return null;

				// Get recent entries for context
				var recentEntries = await _db.BrainDumpEntries
					.Where(e => e.UserId == userId && e.DeletedAtUtc == null && e.CreatedAtUtc >= DateTime.UtcNow.AddDays(-30))
					.OrderByDescending(e => e.CreatedAtUtc)
					.Take(10)
					.ToListAsync();

				var prompt = BrainDumpPromptBuilder.BuildInsightPrompt(entry, recentEntries);
				var response = await _runPodService.SendPromptAsync(prompt, 500, 0.7);
				
				// Parse the response to extract insight
				var insight = BrainDumpPromptBuilder.ParseInsightResponse(response, _logger);
				return insight;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to generate AI insight for brain dump entry {EntryId}", entryId);
				return null;
			}
		}
	}
}

public class ScheduledTask
{
	public TaskSuggestion Suggestion { get; set; } = new();
	public DateTime Date { get; set; }
	public TimeSpan Time { get; set; }
}

public class TimeSlotManager
{
	private readonly (TimeSpan start, TimeSpan end) _weekdaySlots;
	private readonly (TimeSpan start, TimeSpan end) _weekendSlots;
	private readonly Dictionary<DateTime, List<(TimeSpan start, TimeSpan end)>> _reservedSlots;
	private readonly int _bufferMinutes = 15;

	public TimeSlotManager((TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots)
	{
		_weekdaySlots = weekdaySlots;
		_weekendSlots = weekendSlots;
		_reservedSlots = new Dictionary<DateTime, List<(TimeSpan start, TimeSpan end)>>();
	}

	public (DateTime date, TimeSpan time) FindNextAvailableSlot(int durationMinutes)
	{
		var startDate = DateTime.Today.AddDays(1); // Start from tomorrow
		var maxDays = 14; // Look ahead 2 weeks

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = startDate.AddDays(dayOffset);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? _weekendSlots : _weekdaySlots;

			// Find available time within the day's slots
			var availableTime = FindAvailableTimeInDay(date, slots, durationMinutes);
			if (availableTime != TimeSpan.Zero)
			{
				return (date, availableTime);
			}
		}

		// Fallback: return tomorrow at start of available slots
		var fallbackDate = DateTime.Today.AddDays(1);
		var fallbackIsWeekend = fallbackDate.DayOfWeek == DayOfWeek.Saturday || fallbackDate.DayOfWeek == DayOfWeek.Sunday;
		var fallbackSlots = fallbackIsWeekend ? _weekendSlots : _weekdaySlots;
		return (fallbackDate, fallbackSlots.start);
	}

	public (DateTime date, TimeSpan time) FindSlotMatchingTime(TimeSpan preferredTime, int durationMinutes)
	{
		var startDate = DateTime.Today.AddDays(1);
		var maxDays = 14;

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = startDate.AddDays(dayOffset);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? _weekendSlots : _weekdaySlots;

			// Check if preferred time fits within available slots
			if (preferredTime >= slots.start && preferredTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end)
			{
				// Check if this time slot is available
				if (IsTimeSlotAvailable(date, preferredTime, durationMinutes))
				{
					return (date, preferredTime);
				}
			}
		}

		return (DateTime.MinValue, TimeSpan.Zero);
	}

	public void ReserveSlot(DateTime date, TimeSpan time, int durationMinutes)
	{
		if (!_reservedSlots.ContainsKey(date))
		{
			_reservedSlots[date] = new List<(TimeSpan start, TimeSpan end)>();
		}

		var endTime = time.Add(TimeSpan.FromMinutes(durationMinutes + _bufferMinutes));
		_reservedSlots[date].Add((time, endTime));
	}

	private TimeSpan FindAvailableTimeInDay(DateTime date, (TimeSpan start, TimeSpan end) slots, int durationMinutes)
	{
		var currentTime = slots.start;
		var endTime = slots.end;

		while (currentTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= endTime)
		{
			if (IsTimeSlotAvailable(date, currentTime, durationMinutes))
			{
				return currentTime;
			}

			// Move to next 30-minute slot
			currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
		}

		return TimeSpan.Zero;
	}

	private bool IsTimeSlotAvailable(DateTime date, TimeSpan time, int durationMinutes)
	{
		if (!_reservedSlots.ContainsKey(date))
			return true;

		var requestedStart = time;
		var requestedEnd = time.Add(TimeSpan.FromMinutes(durationMinutes + _bufferMinutes));

		foreach (var reservedSlot in _reservedSlots[date])
		{
			// Check for overlap
			if (requestedStart < reservedSlot.end && requestedEnd > reservedSlot.start)
			{
				return false;
			}
		}

		return true;
	}
}



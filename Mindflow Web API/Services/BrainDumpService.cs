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
		Task<List<TaskItem>> AddMultipleTasksToCalendarAsync(Guid userId, List<TaskSuggestion> suggestions, DTOs.WellnessCheckInDto? wellnessData = null, Guid? brainDumpEntryId = null);
		Task<string?> GenerateAiInsightAsync(Guid userId, Guid entryId);
		Task<AnalyticsDto> GetAnalyticsAsync(Guid userId, Guid? brainDumpEntryId = null);
		Task<List<TaskItem>> AutoScheduleAllTasksAsync(Guid userId, Guid brainDumpEntryId, List<Guid>? suggestionIds = null);
		Task<bool> SkipTasksAsync(Guid userId, Guid brainDumpEntryId, List<Guid>? suggestionIds = null);
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
            var emotions = await ExtractEmotionsAsync(request.Text);
            var topics = await ExtractTopicsAsync(request.Text);
            var summary = await SummarizeMindDumpAsync(request.Text);

            // ⚠️ WellnessData is huge — reduce it first
            var wellnessSummary = WellnessReducer.Reduce(wellnessData);

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
				Tags = string.Join(", ", topics),
				IsFavorite = false,
				Source = BrainDumpSource.Web // Brain dump comes from web/mobile app
			};
			_db.BrainDumpEntries.Add(entry);
			await _db.SaveChangesAsync();

			// Multi-prompt approach: Execute steps sequentially
			_logger.LogInformation("Starting multi-prompt approach for brain dump entry {EntryId}", entry.Id);
			
			// Step 1: Extract Key Themes
			_logger.LogDebug("Step 1: Extracting key themes");
			var themesPrompt = BrainDumpPromptBuilder.BuildExtractThemesPrompt(summary, emotions, topics);
			var themesResponse = await _runPodService.SendPromptAsync(themesPrompt, 200, temperature);
			var themes = BrainDumpPromptBuilder.ParseThemesResponse(themesResponse, _logger);
			if (themes.Count == 0)
			{
				_logger.LogWarning("No themes extracted, using fallback themes");
				themes = new List<string> { "General", "Wellness", "Personal" };
			}
			_logger.LogDebug("Extracted {Count} themes: {Themes}", themes.Count, string.Join(", ", themes));

			// Step 2: Generate User Profile (Enhanced with original text)
			_logger.LogDebug("Step 2: Generating user profile");
			var profilePrompt = BrainDumpPromptBuilder.BuildUserProfilePrompt(
				request.Text ?? string.Empty, 
				summary, 
				emotions, 
				userName
			);
			var profileResponse = await _runPodService.SendPromptAsync(profilePrompt, 300, temperature); // Increased tokens for better response
			if (!string.IsNullOrWhiteSpace(profileResponse))
			{
				_logger.LogDebug("Raw profile response received: {Response}", profileResponse.Substring(0, Math.Min(200, profileResponse.Length)));
			}
			else
			{
				_logger.LogWarning("Profile response is null or empty");
			}
			var userProfileSummary = BrainDumpPromptBuilder.ParseUserProfileResponse(profileResponse ?? string.Empty, _logger);
			_logger.LogInformation("Generated user profile: Name={Name}, State={State}, Emoji={Emoji}", 
				userProfileSummary.Name, userProfileSummary.CurrentState, userProfileSummary.Emoji);

			// Step 3: Generate AI Summary (Enhanced with original text for deeper context)
			_logger.LogDebug("Step 3: Generating enhanced AI summary");
			var summaryPrompt = BrainDumpPromptBuilder.BuildAiSummaryPrompt(
				request.Text ?? string.Empty, 
				summary, 
				emotions, 
				themes, 
				request
			);
			var summaryResponse = await _runPodService.SendPromptAsync(summaryPrompt, 400, temperature); // Increased tokens for deeper analysis
			var aiSummary = BrainDumpPromptBuilder.ParseAiSummaryResponse(summaryResponse, _logger);
			_logger.LogDebug("Generated AI summary: {Summary}", aiSummary.Substring(0, Math.Min(100, aiSummary.Length)));

			// Step 4: Generate Task Suggestions (with retry logic)
			_logger.LogDebug("Step 4: Generating task suggestions");
			BrainDumpResponse? brainDumpResponse = null;
			int attempt = 0;
			int maxAttempts = 3;
			double currentTemperature = temperature;
			bool forceMinimumActivities = false;
			List<TaskSuggestion> suggestedActivities = new();

			while (attempt < maxAttempts)
			{
				var tasksPrompt = BrainDumpPromptBuilder.BuildTaskSuggestionsPrompt(
					request.Text ?? string.Empty, // Original text for specific task extraction
					summary,
					emotions,
					topics,
					themes,
					wellnessSummary,
					request,
					forceMinimumActivities
				);
				
				var tasksResponse = await _runPodService.SendPromptAsync(tasksPrompt, maxTokens, currentTemperature);
				suggestedActivities = BrainDumpPromptBuilder.ParseTaskSuggestionsResponse(tasksResponse, _logger);

				// Post-process prioritization scores if missing or inconsistent
				if (suggestedActivities != null && suggestedActivities.Count > 0)
				{
					foreach (var t in suggestedActivities)
					{
						// Normalize urgency/importance casing
						if (!string.IsNullOrWhiteSpace(t.Urgency))
							t.Urgency = NormalizePriorityLevel(t.Urgency);
						if (!string.IsNullOrWhiteSpace(t.Importance))
							t.Importance = NormalizePriorityLevel(t.Importance);

						// Compute priority score if not provided or out of range
						if (!t.PriorityScore.HasValue || t.PriorityScore < 1 || t.PriorityScore > 10)
						{
							var urgencyScore = MapLevelToScore(t.Urgency);
							var importanceScore = MapLevelToScore(t.Importance);
							// Heavier weight on importance
							t.PriorityScore = Math.Clamp(importanceScore * 2 + urgencyScore, 1, 10);
						}
					}

					// Sort tasks by urgency (High > Medium > Low), then by priority score as tiebreaker
					suggestedActivities = suggestedActivities
						.OrderByDescending(t => MapLevelToScore(t.Urgency)) // Urgency first (High=5, Medium=3, Low=1)
						.ThenByDescending(t => t.PriorityScore ?? 0) // Priority score as tiebreaker
						.ToList();
				}
				
				if (suggestedActivities != null && suggestedActivities.Count > 0)
				{
					_logger.LogDebug("Successfully generated {Count} task suggestions", suggestedActivities.Count);
					break; // success
				}

				attempt++;
				_logger.LogWarning("Task suggestions empty (attempt {Attempt}/{Max}). Retrying with stricter settings.", attempt, maxAttempts);
				forceMinimumActivities = true;
				currentTemperature = Math.Max(0.2, currentTemperature - 0.2);
			}

			// Step 4.5: Generate Wellness Tasks (separate from brain dump tasks)
			_logger.LogDebug("Step 4.5: Generating wellness task suggestions");
			List<TaskSuggestion> wellnessTasks = new();
			try
			{
				var wellnessPrompt = BrainDumpPromptBuilder.BuildWellnessTaskSuggestionsPrompt(wellnessSummary, request);
				var wellnessResponse = await _runPodService.SendPromptAsync(wellnessPrompt, 800, temperature);
				wellnessTasks = BrainDumpPromptBuilder.ParseTaskSuggestionsResponse(wellnessResponse, _logger);

				// Post-process wellness tasks (same as brain dump tasks)
				if (wellnessTasks != null && wellnessTasks.Count > 0)
				{
					foreach (var t in wellnessTasks)
					{
						// Normalize urgency/importance casing
						if (!string.IsNullOrWhiteSpace(t.Urgency))
							t.Urgency = NormalizePriorityLevel(t.Urgency);
						if (!string.IsNullOrWhiteSpace(t.Importance))
							t.Importance = NormalizePriorityLevel(t.Importance);

						// Compute priority score if not provided or out of range
						if (!t.PriorityScore.HasValue || t.PriorityScore < 1 || t.PriorityScore > 10)
						{
							var urgencyScore = MapLevelToScore(t.Urgency);
							var importanceScore = MapLevelToScore(t.Importance);
							// Heavier weight on importance
							t.PriorityScore = Math.Clamp(importanceScore * 2 + urgencyScore, 1, 10);
						}
					}

					_logger.LogInformation("Successfully generated {Count} wellness task suggestions", wellnessTasks.Count);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to generate wellness tasks, continuing with brain dump tasks only");
				// Continue without wellness tasks - brain dump tasks will still work
			}

			// Merge brain dump tasks and wellness tasks
			if (wellnessTasks != null && wellnessTasks.Count > 0)
			{
				suggestedActivities = suggestedActivities ?? new List<TaskSuggestion>();
				var brainDumpCount = suggestedActivities.Count;
				suggestedActivities.AddRange(wellnessTasks);
				
				// Re-sort all tasks by urgency (High > Medium > Low), then by priority score as tiebreaker
				suggestedActivities = suggestedActivities
					.OrderByDescending(t => MapLevelToScore(t.Urgency)) // Urgency first (High=5, Medium=3, Low=1)
					.ThenByDescending(t => t.PriorityScore ?? 0) // Priority score as tiebreaker
					.ToList();
				
				_logger.LogInformation("Merged {BrainDumpCount} brain dump tasks with {WellnessCount} wellness tasks (total: {Total}), sorted by urgency", 
					brainDumpCount, wellnessTasks.Count, suggestedActivities.Count);
			}
			else
			{
				// If no wellness tasks, still sort brain dump tasks by urgency
				if (suggestedActivities != null && suggestedActivities.Count > 0)
				{
					suggestedActivities = suggestedActivities
						.OrderByDescending(t => MapLevelToScore(t.Urgency)) // Urgency first
						.ThenByDescending(t => t.PriorityScore ?? 0) // Priority score as tiebreaker
						.ToList();
				}
			}

			// Step 5: Break Down Complex Tasks into Micro-Steps
			if (suggestedActivities != null && suggestedActivities.Count > 0)
			{
				_logger.LogDebug("Step 5: Breaking down complex tasks into micro-steps");
				try
				{
					var breakdownPrompt = BrainDumpPromptBuilder.BuildTaskBreakdownPrompt(suggestedActivities, request.Text ?? string.Empty);
					var breakdownResponse = await _runPodService.SendPromptAsync(breakdownPrompt, 600, temperature);
					var taskBreakdown = BrainDumpPromptBuilder.ParseTaskBreakdownResponse(breakdownResponse, _logger);
					
					// Apply breakdown to tasks
					foreach (var kvp in taskBreakdown)
					{
						if (kvp.Key >= 0 && kvp.Key < suggestedActivities.Count && kvp.Value != null && kvp.Value.Count > 0)
						{
							suggestedActivities[kvp.Key].SubSteps = kvp.Value;
							_logger.LogDebug("Added {Count} sub-steps to task: {Task}", kvp.Value.Count, suggestedActivities[kvp.Key].Task);
						}
					}
					
					var tasksWithSubSteps = taskBreakdown.Count;
					_logger.LogInformation("Successfully broke down {Count} tasks into micro-steps", tasksWithSubSteps);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to break down tasks into micro-steps, continuing without breakdown");
					// Continue without breakdown - tasks will still work
				}
			}

			// Step 6: Generate Emotional Intelligence Layer
			_logger.LogDebug("Step 6: Generating emotional intelligence layer");
			string? emotionalValidation = null;
			string? patternInsight = null;
			List<string>? copingTools = null;
			
			try
			{
				var emotionalIntelligencePrompt = BrainDumpPromptBuilder.BuildEmotionalIntelligencePrompt(
					request.Text ?? string.Empty,
					summary,
					emotions,
					themes,
					request
				);
				var emotionalIntelligenceResponse = await _runPodService.SendPromptAsync(emotionalIntelligencePrompt, 500, temperature);
				var (validation, pattern, tools) = BrainDumpPromptBuilder.ParseEmotionalIntelligenceResponse(emotionalIntelligenceResponse, _logger);
				
				emotionalValidation = validation;
				patternInsight = pattern;
				copingTools = tools;
				
				_logger.LogInformation("Generated emotional intelligence: Validation={HasValidation}, Pattern={HasPattern}, Tools={ToolsCount}", 
					!string.IsNullOrWhiteSpace(emotionalValidation),
					!string.IsNullOrWhiteSpace(patternInsight),
					copingTools?.Count ?? 0);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to generate emotional intelligence layer, continuing without it");
				// Continue without emotional intelligence - response will still work
			}

			// Build the response object
			brainDumpResponse = new BrainDumpResponse
			{
				UserProfile = userProfileSummary,
				KeyThemes = themes,
				AiSummary = aiSummary,
				SuggestedActivities = suggestedActivities ?? new List<TaskSuggestion>(),
				// Emotional Intelligence Layer
				EmotionalValidation = emotionalValidation,
				PatternInsight = patternInsight,
				CopingTools = copingTools
			};

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

			// Gather data for meaningful insights
			_logger.LogDebug("Gathering progress metrics and emotion trends for user {UserId}. Current entry text length: {TextLength}", 
				userId, entry.Text?.Length ?? 0);
			var progressMetrics = await CalculateProgressMetricsAsync(userId);
			var emotionTrends = await AnalyzeEmotionTrendsAsync(userId, entry, emotions); // Pass current entry and extracted emotions
			var insights = GenerateInsights(progressMetrics, emotionTrends, entry); // Pass current entry for fallback
			var patterns = GeneratePatterns(emotionTrends);
			
			_logger.LogInformation("Generated insights: {InsightsCount}, patterns: {PatternsCount}, emotionTrends: {HasEmotionTrends}", 
				insights?.Count ?? 0, patterns?.Count ?? 0, emotionTrends != null);

			// Create personalized message with insights
			var personalizedMessage = BuildPersonalizedMessage(userName, wellnessData, progressMetrics, emotionTrends);

			// Add insights to response
			brainDumpResponse.Insights = insights;
			brainDumpResponse.Patterns = patterns;
			brainDumpResponse.ProgressMetrics = progressMetrics;
			brainDumpResponse.EmotionTrends = emotionTrends;
			brainDumpResponse.PersonalizedMessage = personalizedMessage;
			
			// Add brain dump entry ID for linking tasks
			brainDumpResponse.BrainDumpEntryId = entry.Id;

			// Persist suggestions for later scheduling/skip actions
			await SaveTaskSuggestionRecordsAsync(userId, entry.Id, brainDumpResponse.SuggestedActivities ?? new List<TaskSuggestion>());

			return brainDumpResponse;
		}

		private async Task SaveTaskSuggestionRecordsAsync(Guid userId, Guid brainDumpEntryId, List<TaskSuggestion> suggestions)
		{
			// Remove existing suggestions for this brain dump entry to avoid duplicates
			var existing = await _db.TaskSuggestionRecords
				.Where(r => r.BrainDumpEntryId == brainDumpEntryId && r.UserId == userId)
				.ToListAsync();
			if (existing.Count > 0)
				_db.TaskSuggestionRecords.RemoveRange(existing);

			if (suggestions == null || suggestions.Count == 0)
			{
				await _db.SaveChangesAsync();
				return;
			}

			foreach (var s in suggestions)
			{
				string? subStepsJson = null;
				if (s.SubSteps != null && s.SubSteps.Count > 0)
				{
					try
					{
						subStepsJson = System.Text.Json.JsonSerializer.Serialize(s.SubSteps);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to serialize sub-steps for suggestion {Task}", s.Task);
					}
				}

				var record = new TaskSuggestionRecord
				{
					UserId = userId,
					BrainDumpEntryId = brainDumpEntryId,
					Task = s.Task ?? string.Empty,
					Notes = s.Notes,
					Frequency = s.Frequency ?? string.Empty,
					Duration = s.Duration ?? string.Empty,
					Priority = s.Priority,
					SuggestedTime = s.SuggestedTime,
					Urgency = s.Urgency,
					Importance = s.Importance,
					PriorityScore = s.PriorityScore,
					SubSteps = subStepsJson,
					Status = TaskSuggestionStatus.Suggested,
					CreatedAtUtc = DateTime.UtcNow
				};

				_db.TaskSuggestionRecords.Add(record);
			}

			await _db.SaveChangesAsync();

			// Attach generated record Ids back to suggestions so UI can reference them
			var savedRecords = await _db.TaskSuggestionRecords
				.Where(r => r.BrainDumpEntryId == brainDumpEntryId && r.UserId == userId)
				.OrderBy(r => r.CreatedAtUtc)
				.ToListAsync();

			for (int i = 0; i < suggestions.Count && i < savedRecords.Count; i++)
			{
				suggestions[i].Id = savedRecords[i].Id;
			}
		}

		private static List<TaskSuggestion> GenerateFallbackActivities(BrainDumpRequest request)
		{
			var text = (request.Text ?? string.Empty).ToLower();
			var list = new List<TaskSuggestion>();
			// Basic heuristics to generate 4 concise tasks
			if (text.Contains("anxiety") || text.Contains("stress") || text.Contains("overwhelm"))
			{
				list.Add(new TaskSuggestion
				{
					Task = "Do a 5-minute deep breathing session",
					Frequency = "Once today",
					Duration = "5 minutes",
					Notes = "Helps lower cortisol and reset focus",
					Priority = "High",
					SuggestedTime = "Afternoon",
					Urgency = "High",
					Importance = "High",
					PriorityScore = 9
				});
			}
			if (text.Contains("sleep"))
			{
				list.Add(new TaskSuggestion
				{
					Task = "Prepare a simple wind-down routine",
					Frequency = "Tonight",
					Duration = "10 minutes",
					Notes = "Light stretch, no screens for 30 minutes",
					Priority = "Medium",
					SuggestedTime = "Evening",
					Urgency = "Medium",
					Importance = "High",
					PriorityScore = 7
				});
			}
			if (text.Contains("email") || text.Contains("inbox") || text.Contains("admin"))
			{
				list.Add(new TaskSuggestion
				{
					Task = "Clear your top 5 emails",
					Frequency = "Once today",
					Duration = "15 minutes",
					Notes = "Reply or archive; schedule longer replies",
					Priority = "Medium",
					SuggestedTime = "Morning",
					Urgency = "Medium",
					Importance = "Medium",
					PriorityScore = 5
				});
			}
			if (text.Contains("call") || text.Contains("doctor") || text.Contains("appointment"))
			{
				list.Add(new TaskSuggestion
				{
					Task = "Schedule the pending appointment",
					Frequency = "Once today",
					Duration = "10 minutes",
					Notes = "Pick a date within 2 weeks",
					Priority = "High",
					SuggestedTime = "Morning",
					Urgency = "High",
					Importance = "High",
					PriorityScore = 9
				});
			}
			// Always include two general-purpose wellness tasks if list is short
			if (list.Count < 4)
			{
				list.Add(new TaskSuggestion
				{
					Task = "Take a 10-minute walk",
					Frequency = "Once today",
					Duration = "10 minutes",
					Notes = "Gentle movement boosts mood and clarity",
					Priority = "Medium",
					SuggestedTime = "Afternoon",
					Urgency = "Low",
					Importance = "Medium",
					PriorityScore = 3
				});
			}
			if (list.Count < 4)
			{
				list.Add(new TaskSuggestion
				{
					Task = "Write 3 lines of reflection",
					Frequency = "Once today",
					Duration = "5 minutes",
					Notes = "Capture one worry and one win",
					Priority = "Low",
					SuggestedTime = "Evening",
					Urgency = "Low",
					Importance = "Low",
					PriorityScore = 2
				});
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

			// Ensure taskDate is UTC (explicitly mark as UTC)
			var adjustedDate = DateTime.SpecifyKind(taskDate.Date, DateTimeKind.Utc);
			var adjustedTime = taskTime;
			
			// Get slot boundaries for the adjusted date to ensure we don't exceed them
			var isWeekend = adjustedDate.DayOfWeek == DayOfWeek.Saturday || adjustedDate.DayOfWeek == DayOfWeek.Sunday;
			(TimeSpan slotStart, TimeSpan slotEnd) slotBounds;
			if (wellnessData != null)
			{
				// Use UTC fields if available (they're already converted to UTC)
				if (isWeekend && wellnessData.WeekendStartTimeUtc.HasValue && wellnessData.WeekendEndTimeUtc.HasValue)
				{
					slotBounds = (wellnessData.WeekendStartTimeUtc.Value.TimeOfDay, wellnessData.WeekendEndTimeUtc.Value.TimeOfDay);
				}
				else if (!isWeekend && wellnessData.WeekdayStartTimeUtc.HasValue && wellnessData.WeekdayEndTimeUtc.HasValue)
				{
					slotBounds = (wellnessData.WeekdayStartTimeUtc.Value.TimeOfDay, wellnessData.WeekdayEndTimeUtc.Value.TimeOfDay);
				}
				else if (!string.IsNullOrWhiteSpace(wellnessData.TimezoneId))
				{
					// Fall back to converting local time fields
					if (isWeekend)
					{
						slotBounds = ParseTimeSlotsForDate(
							wellnessData.WeekendStartTime,
							wellnessData.WeekendStartShift,
							wellnessData.WeekendEndTime,
							wellnessData.WeekendEndShift,
							adjustedDate,
							wellnessData.TimezoneId);
					}
					else
					{
						slotBounds = ParseTimeSlotsForDate(
							wellnessData.WeekdayStartTime,
							wellnessData.WeekdayStartShift,
							wellnessData.WeekdayEndTime,
							wellnessData.WeekdayEndShift,
							adjustedDate,
							wellnessData.TimezoneId);
					}
				}
				else
				{
					// No timezone, use default slots (24 hours)
					slotBounds = (TimeSpan.Zero, new TimeSpan(24, 0, 0));
				}
			}
			else
			{
				// No wellness data, use default slots (24 hours)
				slotBounds = (TimeSpan.Zero, new TimeSpan(24, 0, 0));
			}
			
			// Helper function to check if time is within slot bounds (handles midnight crossing)
			bool IsTimeWithinSlot(TimeSpan time, TimeSpan duration, TimeSpan slotStart, TimeSpan slotEnd)
			{
				var timeEnd = time.Add(duration);
				
				// Case 1: Normal slot (start < end, e.g., 07:00 - 10:00)
				if (slotEnd > slotStart)
				{
					return time >= slotStart && timeEnd <= slotEnd;
				}
				
				// Case 2: Slot crosses midnight (start > end, e.g., 23:00 - 02:30)
				// Valid times are:
				// - From slotStart to midnight (23:00 - 24:00)
				// - From midnight to slotEnd (00:00 - 02:30)
				if (time >= slotStart)
				{
					// Time is after start (before midnight), check if entire duration fits before midnight
					// If duration would cross midnight, check if it fits within the slot window
					if (timeEnd.TotalMinutes > 24 * 60)
					{
						// Duration crosses midnight, check if it fits within slot window
						var slotDuration = (24 * 60 - slotStart.TotalMinutes) + slotEnd.TotalMinutes;
						return duration.TotalMinutes <= slotDuration;
					}
					return true; // Fits before midnight
				}
				else if (time <= slotEnd)
				{
					// Time is before end (after midnight), check if entire duration fits
					return timeEnd <= slotEnd;
				}
				else
				{
					// Time is between slotEnd and slotStart (invalid zone for midnight-crossing slots)
					// This is the "dead zone" (e.g., 02:30 - 23:00 is invalid)
					return false;
				}
			}
			
			// Prevent stacking: if chosen time is occupied, calculate next available time
			// based on conflicting task end + buffer, rounded to 30-minute increments
			// BUT ensure we stay within slot boundaries
			var maxAttempts = 1000; // Prevent infinite loops
			var attempts = 0;
			const int bufferMinutes = 15; // Buffer between tasks
			const int incrementMinutes = 30; // Time slot increment
			TimeSpan? lastAttemptedTime = null; // Track last attempted time to detect infinite loops
			var consecutiveSameTimeAttempts = 0; // Track if we're stuck on the same time
			
			_logger.LogDebug("Starting conflict check loop for task. Initial time: {Time}, Slot bounds: {Start} to {End}", 
				adjustedTime, slotBounds.slotStart, slotBounds.slotEnd);
			
			while (
				attempts < maxAttempts &&
				(
					!await IsTimeSlotAvailableAsync(adjustedDate, adjustedTime, durationMinutes, userId)
					|| !IsTimeWithinSlot(adjustedTime, TimeSpan.FromMinutes(durationMinutes), slotBounds.slotStart, slotBounds.slotEnd)
				)
			)
			{
				// Detect if we're stuck on the same time
				if (lastAttemptedTime.HasValue && adjustedTime == lastAttemptedTime.Value)
				{
					consecutiveSameTimeAttempts++;
					if (consecutiveSameTimeAttempts >= 3)
					{
						// We're stuck - force advance by increment
						_logger.LogWarning("Detected infinite loop: stuck on time {Time} for {Attempts} attempts. Forcing advance.", 
							adjustedTime, consecutiveSameTimeAttempts);
						adjustedTime = adjustedTime.Add(TimeSpan.FromMinutes(incrementMinutes));
						consecutiveSameTimeAttempts = 0;
					}
				}
				else
				{
					consecutiveSameTimeAttempts = 0;
				}
				
				lastAttemptedTime = adjustedTime;
				
				// Find the next available time by checking for conflicting tasks
				var nextAvailableTime = await CalculateNextAvailableTimeAsync(adjustedDate, adjustedTime, durationMinutes, userId, bufferMinutes, incrementMinutes);
				
				if (nextAvailableTime.HasValue)
				{
					// Ensure we're actually advancing forward
					if (nextAvailableTime.Value <= adjustedTime)
					{
						// Calculated time is not forward - force advance
						_logger.LogWarning("Calculated next available time {NextTime} is not after current time {CurrentTime}. Forcing advance.", 
							nextAvailableTime.Value, adjustedTime);
						adjustedTime = adjustedTime.Add(TimeSpan.FromMinutes(incrementMinutes));
					}
					else
					{
						_logger.LogDebug("Time {Time} is not available. Attempt {Attempt}. Calculated next available time: {NextTime}", 
							adjustedTime, attempts + 1, nextAvailableTime.Value);
						adjustedTime = nextAvailableTime.Value;
					}
				}
				else
				{
					// Fallback: move forward by 30 minutes if we can't calculate next available time
					_logger.LogDebug("Time {Time} is not available. Attempt {Attempt}. Moving forward by 30 minutes (fallback).", 
						adjustedTime, attempts + 1);
					adjustedTime = adjustedTime.Add(TimeSpan.FromMinutes(incrementMinutes));
				}
				
				// Check if we've exceeded the slot end time (handle midnight crossing)
				bool exceededSlot = false;
				var timeEnd = adjustedTime.Add(TimeSpan.FromMinutes(durationMinutes));
				
				if (slotBounds.slotEnd > slotBounds.slotStart)
				{
					// Normal slot: check if time exceeds end
					exceededSlot = timeEnd > slotBounds.slotEnd;
				}
				else
				{
					// Slot crosses midnight: we've exceeded if:
					// - We're past the end time (after midnight portion) AND
					// - We're before the start time (before evening portion) AND  
					// - The task end would also be past end
					// This means we're in the "dead zone" between slotEnd and slotStart
					exceededSlot = adjustedTime > slotBounds.slotEnd && adjustedTime < slotBounds.slotStart;
				}
				
				if (exceededSlot)
				{
					// Move to next day and reset to slot start
					adjustedDate = DateTime.SpecifyKind(adjustedDate.AddDays(1).Date, DateTimeKind.Utc);
					isWeekend = adjustedDate.DayOfWeek == DayOfWeek.Saturday || adjustedDate.DayOfWeek == DayOfWeek.Sunday;
					
					// Recalculate slot bounds for the new date
					if (wellnessData != null)
					{
						// Use UTC fields if available (they're already converted to UTC)
						if (isWeekend && wellnessData.WeekendStartTimeUtc.HasValue && wellnessData.WeekendEndTimeUtc.HasValue)
						{
							slotBounds = (wellnessData.WeekendStartTimeUtc.Value.TimeOfDay, wellnessData.WeekendEndTimeUtc.Value.TimeOfDay);
						}
						else if (!isWeekend && wellnessData.WeekdayStartTimeUtc.HasValue && wellnessData.WeekdayEndTimeUtc.HasValue)
						{
							slotBounds = (wellnessData.WeekdayStartTimeUtc.Value.TimeOfDay, wellnessData.WeekdayEndTimeUtc.Value.TimeOfDay);
						}
						else if (!string.IsNullOrWhiteSpace(wellnessData.TimezoneId))
						{
							// Fall back to converting local time fields
							if (isWeekend)
							{
								slotBounds = ParseTimeSlotsForDate(
									wellnessData.WeekendStartTime,
									wellnessData.WeekendStartShift,
									wellnessData.WeekendEndTime,
									wellnessData.WeekendEndShift,
									adjustedDate,
									wellnessData.TimezoneId);
							}
							else
							{
								slotBounds = ParseTimeSlotsForDate(
									wellnessData.WeekdayStartTime,
									wellnessData.WeekdayStartShift,
									wellnessData.WeekdayEndTime,
									wellnessData.WeekdayEndShift,
									adjustedDate,
									wellnessData.TimezoneId);
							}
						}
					}
					
					adjustedTime = slotBounds.slotStart;
				}
				
				// Handle day boundary crossing (fallback)
				// Only move to next day if we're not in a midnight-crossing slot
				// For midnight-crossing slots, times after midnight (00:00-XX:XX) are valid on the same date
				if (adjustedTime.TotalMinutes >= 24 * 60)
				{
					// Check if we're in a midnight-crossing slot
					if (slotBounds.slotEnd <= slotBounds.slotStart && adjustedTime <= slotBounds.slotEnd)
					{
						// We're in the after-midnight portion of a midnight-crossing slot
						// Wrap time back to the after-midnight portion (already done by TimeSpan arithmetic)
						adjustedTime = new TimeSpan(0, (int)(adjustedTime.TotalMinutes % (24 * 60)), 0);
					}
					else
					{
						// Normal case: move to next day
						adjustedDate = DateTime.SpecifyKind(adjustedDate.AddDays(1).Date, DateTimeKind.Utc);
						adjustedTime = TimeSpan.Zero;
					}
				}
				
				attempts++;
			}
			
			// If we couldn't find a valid slot, throw an exception
			if (attempts >= maxAttempts)
			{
				_logger.LogWarning("Could not find available slot for task '{Task}' after {Attempts} attempts", 
					request.Task, attempts);
				throw new InvalidOperationException($"Could not find an available time slot for the task within the user's wellness schedule after {attempts} attempts.");
			}

			// Compose UTC datetime for storage/return
			// Both date and time are already in UTC format from the scheduling logic
			var utcDateTime = DateTime.SpecifyKind(adjustedDate.Date.Add(adjustedTime), DateTimeKind.Utc);

			_logger.LogInformation("Creating task '{TaskTitle}' for user {UserId} at {TaskDate} {TaskTime} UTC", 
				request.Task, userId, adjustedDate.ToString("yyyy-MM-dd"), adjustedTime);

			// Extract brain dump linking information if provided
			string? sourceTextExcerpt = null;
			string? lifeArea = null;
			string? emotionTag = null;
			
			if (request.BrainDumpEntryId.HasValue)
			{
				var brainDumpEntry = await _db.BrainDumpEntries
					.FirstOrDefaultAsync(e => e.Id == request.BrainDumpEntryId.Value && e.UserId == userId);
				
				if (brainDumpEntry != null)
				{
					// Extract source text excerpt that relates to this task
					sourceTextExcerpt = ExtractSourceTextExcerpt(brainDumpEntry.Text, request.Task, request.Notes);
					
					// Determine life area and emotion tag from brain dump and task
					lifeArea = DetermineLifeArea(brainDumpEntry.Text, request.Task, request.Notes);
					emotionTag = DetermineEmotionTag(brainDumpEntry.Text, brainDumpEntry.Tags);
					
					_logger.LogDebug("Linking task '{TaskTitle}' to brain dump entry {EntryId}. LifeArea: {LifeArea}, EmotionTag: {EmotionTag}", 
						request.Task, request.BrainDumpEntryId.Value, lifeArea, emotionTag);
				}
			}

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
				IsActive = true,
				// Brain dump linking fields
				SourceBrainDumpEntryId = request.BrainDumpEntryId,
				SourceTextExcerpt = sourceTextExcerpt,
				LifeArea = lifeArea,
				EmotionTag = emotionTag,
				// Prioritization (from AI suggestion, if provided)
				Urgency = request.Urgency,
				Importance = request.Importance,
				PriorityScore = request.PriorityScore
			};

			_db.Tasks.Add(taskItem);
			await _db.SaveChangesAsync();

			return taskItem;
		}

		public async Task<List<TaskItem>> AddMultipleTasksToCalendarAsync(Guid userId, List<TaskSuggestion> suggestions, DTOs.WellnessCheckInDto? wellnessData = null, Guid? brainDumpEntryId = null)
		{
			var createdTasks = new List<TaskItem>();
			
			// Get user's wellness data if not provided
			if (wellnessData == null)
				wellnessData = await _wellnessService.GetAsync(userId);

			// Get brain dump entry if provided for linking
			BrainDumpEntry? brainDumpEntry = null;
			if (brainDumpEntryId.HasValue)
			{
				brainDumpEntry = await _db.BrainDumpEntries
					.FirstOrDefaultAsync(e => e.Id == brainDumpEntryId.Value && e.UserId == userId);
			}

			// Sort tasks by priority (High -> Medium -> Low)
			var sortedSuggestions = suggestions.OrderBy(s => s.Priority switch
			{
				"High" => 1,
				"Medium" => 2,
				"Low" => 3,
				_ => 2
			}).ToList();

			// Schedule tasks across available time slots
			var scheduledTasks = await ScheduleTasksAcrossTimeSlotsAsync(sortedSuggestions, wellnessData, userId);

			foreach (var scheduledTask in scheduledTasks)
			{
				// Ensure tasks in this batch do not stack at the same time; nudge forward in 30-min steps
				var durationMinutes = ParseDurationToMinutes(scheduledTask.Suggestion.Duration);
				// Ensure candidateDate is UTC
				var candidateDate = DateTime.SpecifyKind(scheduledTask.Date.Date, DateTimeKind.Utc);
				var candidateTime = scheduledTask.Time;
				
				// Get slot boundaries for the candidate date to ensure we don't exceed them
				var isWeekend = candidateDate.DayOfWeek == DayOfWeek.Saturday || candidateDate.DayOfWeek == DayOfWeek.Sunday;
				(TimeSpan slotStart, TimeSpan slotEnd) slotBounds;
				if (wellnessData != null)
				{
					// Use UTC fields if available (they're already converted to UTC)
					if (isWeekend && wellnessData.WeekendStartTimeUtc.HasValue && wellnessData.WeekendEndTimeUtc.HasValue)
					{
						slotBounds = (wellnessData.WeekendStartTimeUtc.Value.TimeOfDay, wellnessData.WeekendEndTimeUtc.Value.TimeOfDay);
					}
					else if (!isWeekend && wellnessData.WeekdayStartTimeUtc.HasValue && wellnessData.WeekdayEndTimeUtc.HasValue)
					{
						slotBounds = (wellnessData.WeekdayStartTimeUtc.Value.TimeOfDay, wellnessData.WeekdayEndTimeUtc.Value.TimeOfDay);
					}
					else if (!string.IsNullOrWhiteSpace(wellnessData.TimezoneId))
					{
						// Fall back to converting local time fields
						if (isWeekend)
						{
							slotBounds = ParseTimeSlotsForDate(
								wellnessData.WeekendStartTime,
								wellnessData.WeekendStartShift,
								wellnessData.WeekendEndTime,
								wellnessData.WeekendEndShift,
								candidateDate,
								wellnessData.TimezoneId);
						}
						else
						{
							slotBounds = ParseTimeSlotsForDate(
								wellnessData.WeekdayStartTime,
								wellnessData.WeekdayStartShift,
								wellnessData.WeekdayEndTime,
								wellnessData.WeekdayEndShift,
								candidateDate,
								wellnessData.TimezoneId);
						}
					}
					else
					{
						// No timezone, use default slots (24 hours)
						slotBounds = (TimeSpan.Zero, new TimeSpan(24, 0, 0));
					}
				}
				else
				{
					// No wellness data, use default slots (24 hours)
					slotBounds = (TimeSpan.Zero, new TimeSpan(24, 0, 0));
				}
				
				// Helper function to check if time conflicts with created tasks in this batch
				bool ConflictsWithCreatedTasks(DateTime date, TimeSpan time, int duration)
				{
					var taskStart = date.Date.Add(time);
					var taskEnd = taskStart.AddMinutes(duration);
					
					foreach (var existingTask in createdTasks)
					{
						if (existingTask.Date.Date == date.Date)
						{
							var existingStart = existingTask.Time;
							var existingEnd = existingStart.AddMinutes(existingTask.DurationMinutes);
							
							// Check for overlap with 15-minute buffer
							var bufferMinutes = 15;
							var taskStartWithBuffer = taskStart.AddMinutes(-bufferMinutes);
							var taskEndWithBuffer = taskEnd.AddMinutes(bufferMinutes);
							var existingStartWithBuffer = existingStart.AddMinutes(-bufferMinutes);
							var existingEndWithBuffer = existingEnd.AddMinutes(bufferMinutes);
							
							// Two tasks conflict if their buffered time ranges overlap
							if ((taskStartWithBuffer < existingEndWithBuffer) && (taskEndWithBuffer > existingStartWithBuffer))
							{
								return true;
							}
						}
					}
					return false;
				}
				
				// Helper function to check if time is within slot bounds (handles midnight crossing)
				bool IsTimeWithinSlot(TimeSpan time, TimeSpan duration, TimeSpan slotStart, TimeSpan slotEnd)
				{
					var timeEnd = time.Add(duration);
					
					// Case 1: Normal slot (start < end, e.g., 07:00 - 10:00)
					if (slotEnd > slotStart)
					{
						return time >= slotStart && timeEnd <= slotEnd;
					}
					
					// Case 2: Slot crosses midnight (start > end, e.g., 23:00 - 02:30)
					if (time >= slotStart)
					{
						// Time is after start (before midnight)
						if (timeEnd.TotalMinutes > 24 * 60)
						{
							// Duration crosses midnight, check if it fits within slot window
							var slotDuration = (24 * 60 - slotStart.TotalMinutes) + slotEnd.TotalMinutes;
							return duration.TotalMinutes <= slotDuration;
						}
						return true; // Fits before midnight
					}
					else if (time <= slotEnd)
					{
						// Time is before end (after midnight)
						return timeEnd <= slotEnd;
					}
					else
					{
						// Time is between slotEnd and slotStart (invalid zone)
						return false;
					}
				}
				
				// TimeSlotManager already scheduled this task avoiding conflicts with existing DB tasks
				// We only need to check for conflicts with tasks created in this batch and ensure it's within slot boundaries
				// Skip the database check since TimeSlotManager already handled that
				var maxAttempts = 1000; // Prevent infinite loops
				var attempts = 0;
				while (
					attempts < maxAttempts &&
					(
						ConflictsWithCreatedTasks(candidateDate, candidateTime, durationMinutes)
						|| !IsTimeWithinSlot(candidateTime, TimeSpan.FromMinutes(durationMinutes), slotBounds.slotStart, slotBounds.slotEnd)
					)
				)
				{
					candidateTime = candidateTime.Add(TimeSpan.FromMinutes(30));
					
					// Check if we've exceeded the slot end time (handle midnight crossing)
					bool exceededSlot = false;
					var timeEnd = candidateTime.Add(TimeSpan.FromMinutes(durationMinutes));
					
					if (slotBounds.slotEnd > slotBounds.slotStart)
					{
						// Normal slot: check if time exceeds end
						exceededSlot = timeEnd > slotBounds.slotEnd;
					}
					else
					{
						// Slot crosses midnight: we've exceeded if we're past end and before start
						exceededSlot = candidateTime > slotBounds.slotEnd && candidateTime < slotBounds.slotStart;
					}
					
					if (exceededSlot)
					{
						// Move to next day and reset to slot start
						candidateDate = DateTime.SpecifyKind(candidateDate.AddDays(1).Date, DateTimeKind.Utc);
						isWeekend = candidateDate.DayOfWeek == DayOfWeek.Saturday || candidateDate.DayOfWeek == DayOfWeek.Sunday;
						
						// Recalculate slot bounds for the new date
						if (wellnessData != null && !string.IsNullOrWhiteSpace(wellnessData.TimezoneId))
						{
							if (isWeekend)
							{
								slotBounds = ParseTimeSlotsForDate(
									wellnessData.WeekendStartTime,
									wellnessData.WeekendStartShift,
									wellnessData.WeekendEndTime,
									wellnessData.WeekendEndShift,
									candidateDate,
									wellnessData.TimezoneId);
							}
							else
							{
								slotBounds = ParseTimeSlotsForDate(
									wellnessData.WeekdayStartTime,
									wellnessData.WeekdayStartShift,
									wellnessData.WeekdayEndTime,
									wellnessData.WeekdayEndShift,
									candidateDate,
									wellnessData.TimezoneId);
							}
						}
						
						candidateTime = slotBounds.slotStart;
					}
					
					// Handle day boundary crossing (fallback)
					if (candidateTime.TotalMinutes >= 24 * 60)
					{
						candidateDate = DateTime.SpecifyKind(candidateDate.AddDays(1).Date, DateTimeKind.Utc);
						candidateTime = TimeSpan.Zero;
					}
					
					attempts++;
				}
				
				// Skip this task if we couldn't find a valid slot
				if (attempts >= maxAttempts)
				{
					_logger.LogWarning("Could not find available slot for task '{Task}' after {Attempts} attempts, skipping", 
						scheduledTask.Suggestion.Task, attempts);
					continue;
				}

				// Extract brain dump linking information if available
				string? sourceTextExcerpt = null;
				string? lifeArea = null;
				string? emotionTag = null;
				
				if (brainDumpEntry != null)
				{
					sourceTextExcerpt = ExtractSourceTextExcerpt(brainDumpEntry.Text, scheduledTask.Suggestion.Task, scheduledTask.Suggestion.Notes);
					lifeArea = DetermineLifeArea(brainDumpEntry.Text, scheduledTask.Suggestion.Task, scheduledTask.Suggestion.Notes);
					emotionTag = DetermineEmotionTag(brainDumpEntry.Text, brainDumpEntry.Tags);
				}

				// Serialize sub-steps to JSON string for storage
				string? subStepsJson = null;
				if (scheduledTask.Suggestion.SubSteps != null && scheduledTask.Suggestion.SubSteps.Count > 0)
				{
					try
					{
						subStepsJson = System.Text.Json.JsonSerializer.Serialize(scheduledTask.Suggestion.SubSteps);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to serialize sub-steps for task: {Task}", scheduledTask.Suggestion.Task);
					}
				}
				
				var taskItem = new TaskItem
				{
					UserId = userId,
					Title = scheduledTask.Suggestion.Task,
					Description = scheduledTask.Suggestion.Notes,
					Category = DetermineTaskCategory(scheduledTask.Suggestion.Task, scheduledTask.Suggestion.Notes),
					OtherCategoryName = "AI Suggested",
					Date = DateTime.SpecifyKind(candidateDate.Date.Add(candidateTime), DateTimeKind.Utc).Date,
					Time = DateTime.SpecifyKind(candidateDate.Date.Add(candidateTime), DateTimeKind.Utc),
					DurationMinutes = durationMinutes,
					ReminderEnabled = true,
					RepeatType = MapFrequencyToRepeatType(scheduledTask.Suggestion.Frequency),
					CreatedBySuggestionEngine = true,
					IsApproved = true,
					Status = Models.TaskStatus.Pending,
					IsTemplate = false,
					IsActive = true,
					// Brain dump linking fields
					SourceBrainDumpEntryId = brainDumpEntryId,
					SourceTextExcerpt = sourceTextExcerpt,
					LifeArea = lifeArea,
					EmotionTag = emotionTag,
					// Micro-step breakdown
					SubSteps = subStepsJson,
					// Prioritization (from AI suggestion)
					Urgency = scheduledTask.Suggestion.Urgency,
					Importance = scheduledTask.Suggestion.Importance,
					PriorityScore = scheduledTask.Suggestion.PriorityScore
				};

				_db.Tasks.Add(taskItem);
				createdTasks.Add(taskItem);
			}

			await _db.SaveChangesAsync();
			return createdTasks;
		}

		public async Task<List<TaskItem>> AutoScheduleAllTasksAsync(Guid userId, Guid brainDumpEntryId, List<Guid>? suggestionIds = null)
		{
			// Fetch pending suggestions for this brain dump entry
			var query = _db.TaskSuggestionRecords
				.Where(r => r.UserId == userId && r.BrainDumpEntryId == brainDumpEntryId && r.Status == TaskSuggestionStatus.Suggested);

			if (suggestionIds != null && suggestionIds.Count > 0)
				query = query.Where(r => suggestionIds.Contains(r.Id));

			var records = await query.ToListAsync();
			if (records.Count == 0)
			{
				_logger.LogInformation("No pending suggestions to auto-schedule for BrainDumpEntry {BrainDumpEntryId}", brainDumpEntryId);
				return new List<TaskItem>();
			}

			var created = new List<TaskItem>();

			foreach (var record in records)
			{
				var request = new AddToCalendarRequest
				{
					Task = record.Task,
					Notes = record.Notes,
					Frequency = record.Frequency,
					Duration = record.Duration,
					BrainDumpEntryId = record.BrainDumpEntryId,
					Urgency = record.Urgency,
					Importance = record.Importance,
					PriorityScore = record.PriorityScore,
					ReminderEnabled = false
				};

				var taskItem = await AddTaskToCalendarAsync(userId, request);
				record.Status = TaskSuggestionStatus.Scheduled;
				record.TaskItemId = taskItem.Id;
				created.Add(taskItem);
			}

			await _db.SaveChangesAsync();
			return created;
		}

		public async Task<bool> SkipTasksAsync(Guid userId, Guid brainDumpEntryId, List<Guid>? suggestionIds = null)
		{
			var query = _db.TaskSuggestionRecords
				.Where(r => r.UserId == userId && r.BrainDumpEntryId == brainDumpEntryId && r.Status == TaskSuggestionStatus.Suggested);

			if (suggestionIds != null && suggestionIds.Count > 0)
				query = query.Where(r => suggestionIds.Contains(r.Id));

			var records = await query.ToListAsync();
			if (records.Count == 0)
				return false;

			foreach (var record in records)
				record.Status = TaskSuggestionStatus.Skipped;

			await _db.SaveChangesAsync();
			return true;
		}

	private async Task<List<ScheduledTask>> ScheduleTasksAcrossTimeSlotsAsync(List<TaskSuggestion> suggestions, DTOs.WellnessCheckInDto? wellnessData, Guid userId)
	{
		var scheduledTasks = new List<ScheduledTask>();
		
		_logger.LogInformation("Starting task scheduling for {TaskCount} tasks", suggestions.Count);
		
		// Use UTC fields if available (they're already converted to UTC)
		// Otherwise fall back to converting local time fields
		(TimeSpan start, TimeSpan end) weekdaySlots;
		(TimeSpan start, TimeSpan end) weekendSlots;
		
		if (wellnessData?.WeekdayStartTimeUtc.HasValue == true && wellnessData?.WeekdayEndTimeUtc.HasValue == true)
		{
			// Use UTC fields directly - extract time portion
			// Note: The UTC DateTime may have a date component, but we only need the time
			var startTime = wellnessData.WeekdayStartTimeUtc.Value.TimeOfDay;
			var endTime = wellnessData.WeekdayEndTimeUtc.Value.TimeOfDay;
			
			// Log the raw UTC DateTime values for debugging
			_logger.LogInformation("[SCHEDULING] Raw UTC DateTime values - Start: {StartUtc}, End: {EndUtc}", 
				wellnessData.WeekdayStartTimeUtc.Value, wellnessData.WeekdayEndTimeUtc.Value);
			
			// If end time is earlier than start time, it means the slot crosses midnight
			// This is correct - e.g., 18:00 to 03:00 means 6 PM to 3 AM next day
			weekdaySlots = (startTime, endTime);
			_logger.LogInformation("[SCHEDULING] Using UTC fields for weekday slots: {Start} to {End} (crosses midnight: {CrossesMidnight}, timezone: {Timezone})", 
				weekdaySlots.start, weekdaySlots.end, endTime < startTime, wellnessData.TimezoneId);
		}
		else
		{
			// Fall back to converting local time fields
			var defaultDate = DateTime.UtcNow.Date.AddDays(1);
			_logger.LogInformation("[DEBUG] Wellness data: WeekdayStartTime='{WeekdayStartTime}' {WeekdayStartShift}, TimezoneId='{TimezoneId}'", 
				wellnessData?.WeekdayStartTime, wellnessData?.WeekdayStartShift, wellnessData?.TimezoneId);
			weekdaySlots = ParseTimeSlotsForDate(
				wellnessData?.WeekdayStartTime,
				wellnessData?.WeekdayStartShift,
				wellnessData?.WeekdayEndTime,
				wellnessData?.WeekdayEndShift,
				defaultDate,
				wellnessData?.TimezoneId);
		}
		
		if (wellnessData?.WeekendStartTimeUtc.HasValue == true && wellnessData?.WeekendEndTimeUtc.HasValue == true)
		{
			// Use UTC fields directly - extract time portion
			// Note: The UTC DateTime may have a date component, but we only need the time
			var startTime = wellnessData.WeekendStartTimeUtc.Value.TimeOfDay;
			var endTime = wellnessData.WeekendEndTimeUtc.Value.TimeOfDay;
			
			// Log the raw UTC DateTime values for debugging
			_logger.LogInformation("[SCHEDULING] Raw UTC DateTime values - Start: {StartUtc}, End: {EndUtc}", 
				wellnessData.WeekendStartTimeUtc.Value, wellnessData.WeekendEndTimeUtc.Value);
			
			// If end time is earlier than start time, it means the slot crosses midnight
			// This is correct - e.g., 16:00 to 00:00 means 4 PM to midnight
			weekendSlots = (startTime, endTime);
			_logger.LogInformation("[SCHEDULING] Using UTC fields for weekend slots: {Start} to {End} (crosses midnight: {CrossesMidnight}, timezone: {Timezone})", 
				weekendSlots.start, weekendSlots.end, endTime < startTime, wellnessData.TimezoneId);
		}
		else
		{
			// Fall back to converting local time fields
			var defaultDate = DateTime.UtcNow.Date.AddDays(1);
			_logger.LogInformation("[DEBUG] Wellness data: WeekendStartTime='{WeekendStartTime}' {WeekendStartShift}, TimezoneId='{TimezoneId}'", 
				wellnessData?.WeekendStartTime, wellnessData?.WeekendStartShift, wellnessData?.TimezoneId);
			weekendSlots = ParseTimeSlotsForDate(
				wellnessData?.WeekendStartTime,
				wellnessData?.WeekendStartShift,
				wellnessData?.WeekendEndTime,
				wellnessData?.WeekendEndShift,
				defaultDate,
				wellnessData?.TimezoneId);
		}

		_logger.LogInformation("Weekday slots (UTC): {WeekdayStart} to {WeekdayEnd}", weekdaySlots.start, weekdaySlots.end);
		_logger.LogInformation("Weekend slots (UTC): {WeekendStart} to {WeekendEnd}", weekendSlots.start, weekendSlots.end);

		// Load existing tasks from database to avoid conflicts
		var today = DateTime.UtcNow.Date;
		var lookAheadDays = 14; // Look ahead 2 weeks
		var existingTasks = await _db.Tasks
			.Where(t => t.UserId == userId 
				&& t.IsActive 
				&& t.Time >= today 
				&& t.Time < today.AddDays(lookAheadDays))
			.ToListAsync();
		
		_logger.LogInformation("Loaded {Count} existing tasks from database to avoid conflicts", existingTasks.Count);

		// Create a time slot manager to track available slots
		var slotManager = new TimeSlotManager(weekdaySlots, weekendSlots, _logger);
		
		// Pre-populate TimeSlotManager with existing tasks
		_logger.LogInformation("Pre-populating TimeSlotManager with {Count} existing tasks", existingTasks.Count);
		foreach (var existingTask in existingTasks)
		{
			var taskStart = existingTask.Time;
			if (taskStart.Kind != DateTimeKind.Utc)
			{
				taskStart = DateTime.SpecifyKind(taskStart, DateTimeKind.Utc);
			}
			var taskDate = taskStart.Date;
			var taskTime = taskStart.TimeOfDay;
			var duration = existingTask.DurationMinutes;
			
			_logger.LogInformation("Pre-reserving existing task - Date: {Date}, Time: {Time}, Duration: {Duration}min, TaskId: {TaskId}", 
				taskDate.ToString("yyyy-MM-dd"), taskTime, duration, existingTask.Id);
			slotManager.ReserveSlot(taskDate, taskTime, duration);
		}
		_logger.LogInformation("Finished pre-populating TimeSlotManager with existing tasks");
		
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
			// Use UTC fields if available (they're already converted to UTC)
			// Otherwise fall back to converting local time fields
			(TimeSpan start, TimeSpan end) weekdaySlots;
			(TimeSpan start, TimeSpan end) weekendSlots;
			
		if (wellnessData?.WeekdayStartTimeUtc.HasValue == true && wellnessData?.WeekdayEndTimeUtc.HasValue == true)
		{
			// Use UTC fields directly - extract time portion
			// Note: The UTC DateTime may have a date component, but we only need the time
			var startTime = wellnessData.WeekdayStartTimeUtc.Value.TimeOfDay;
			var endTime = wellnessData.WeekdayEndTimeUtc.Value.TimeOfDay;
			
			// Log the raw UTC DateTime values for debugging
			_logger.LogInformation("[SCHEDULING] Raw UTC DateTime values (Weekday) - Start: {StartUtc}, End: {EndUtc}", 
				wellnessData.WeekdayStartTimeUtc.Value, wellnessData.WeekdayEndTimeUtc.Value);
			
			// If end time is earlier than start time, it means the slot crosses midnight
			// This is correct - e.g., 18:00 to 03:00 means 6 PM to 3 AM next day
			weekdaySlots = (startTime, endTime);
			_logger.LogInformation("[SCHEDULING] Using UTC fields for weekday slots: {Start} to {End} (crosses midnight: {CrossesMidnight}, timezone: {Timezone})", 
				weekdaySlots.start, weekdaySlots.end, endTime < startTime, wellnessData.TimezoneId);
		}
			else
			{
				// Fall back to converting local time fields
				var defaultDate = DateTime.UtcNow.Date.AddDays(1);
				weekdaySlots = ParseTimeSlotsForDate(
					wellnessData?.WeekdayStartTime,
					wellnessData?.WeekdayStartShift,
					wellnessData?.WeekdayEndTime,
					wellnessData?.WeekdayEndShift,
					defaultDate,
					wellnessData?.TimezoneId);
			}
			
		if (wellnessData?.WeekendStartTimeUtc.HasValue == true && wellnessData?.WeekendEndTimeUtc.HasValue == true)
		{
			// Use UTC fields directly - extract time portion
			// Note: The UTC DateTime may have a date component, but we only need the time
			var startTime = wellnessData.WeekendStartTimeUtc.Value.TimeOfDay;
			var endTime = wellnessData.WeekendEndTimeUtc.Value.TimeOfDay;
			
			// Log the raw UTC DateTime values for debugging
			_logger.LogInformation("[SCHEDULING] Raw UTC DateTime values (Weekend) - Start: {StartUtc}, End: {EndUtc}", 
				wellnessData.WeekendStartTimeUtc.Value, wellnessData.WeekendEndTimeUtc.Value);
			
			// If end time is earlier than start time, it means the slot crosses midnight
			// This is correct - e.g., 16:00 to 00:00 means 4 PM to midnight
			weekendSlots = (startTime, endTime);
			_logger.LogInformation("[SCHEDULING] Using UTC fields for weekend slots: {Start} to {End} (crosses midnight: {CrossesMidnight}, timezone: {Timezone})", 
				weekendSlots.start, weekendSlots.end, endTime < startTime, wellnessData.TimezoneId);
		}
			else
			{
				// Fall back to converting local time fields
				var defaultDate = DateTime.UtcNow.Date.AddDays(1);
				weekendSlots = ParseTimeSlotsForDate(
					wellnessData?.WeekendStartTime,
					wellnessData?.WeekendStartShift,
					wellnessData?.WeekendEndTime,
					wellnessData?.WeekendEndShift,
					defaultDate,
					wellnessData?.TimezoneId);
			}

		_logger.LogInformation("[SCHEDULING] Determining optimal schedule for task with duration {Duration} minutes", durationMinutes);
		_logger.LogInformation("[SCHEDULING] Weekday slots (UTC): {WeekdayStart} to {WeekdayEnd}", weekdaySlots.start, weekdaySlots.end);
		_logger.LogInformation("[SCHEDULING] Weekend slots (UTC): {WeekendStart} to {WeekendEnd}", weekendSlots.start, weekendSlots.end);

		// Calculate optimal date and time based on available slots
		// No user preferences - find optimal date and time using wellness data slots
		_logger.LogInformation("[SCHEDULING] Calculating optimal date and time based on available slots");
		var (optimalDate, optimalTime) = await FindNextAvailableSlotAsync(durationMinutes, weekdaySlots, weekendSlots, userId, wellnessData);
		_logger.LogInformation("[SCHEDULING] Found optimal slot: {Date} at {Time}", optimalDate.ToString("yyyy-MM-dd"), optimalTime);
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
				
				_logger.LogDebug("Checking availability for new task: {StartTime} to {EndTime} (Date: {Date})", 
					newTaskStart, newTaskEnd, date.Date);
				
				// Check for existing tasks that overlap with this time slot
				// Compare using both Date field and Time field to catch all conflicts
				var conflictingTasks = await _db.Tasks
					.Where(t => t.UserId == userId 
						&& t.IsActive 
						&& (t.Date.Date == newTaskStart.Date || t.Time.Date == newTaskStart.Date))
					.ToListAsync();
				
				_logger.LogDebug("Found {Count} existing tasks on date {Date} to check for conflicts", 
					conflictingTasks.Count, newTaskStart.Date);
				
				foreach (var existingTask in conflictingTasks)
				{
					// Use the Time field (full DateTime) for comparison, as it's more accurate
					var existingTaskStart = existingTask.Time;
					// Ensure it's treated as UTC
					if (existingTaskStart.Kind != DateTimeKind.Utc)
					{
						existingTaskStart = DateTime.SpecifyKind(existingTaskStart, DateTimeKind.Utc);
					}
					var existingTaskEnd = existingTaskStart.AddMinutes(existingTask.DurationMinutes);
					
					_logger.LogDebug("Checking against existing task: {StartTime} to {EndTime} (Task ID: {TaskId})", 
						existingTaskStart, existingTaskEnd, existingTask.Id);
					
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
						_logger.LogWarning("CONFLICT DETECTED: New task {NewStart}-{NewEnd} overlaps with existing task {ExistingStart}-{ExistingEnd} (Task ID: {TaskId})", 
							newTaskStartWithBuffer, newTaskEndWithBuffer, existingTaskStartWithBuffer, existingTaskEndWithBuffer, existingTask.Id);
						return false;
					}
				}
				
				_logger.LogDebug("No conflicts found for time slot {Time} on date {Date}", time, date.Date);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking time slot availability for {Date} at {Time}", date, time);
				return false; // Conservative approach - assume slot is not available if we can't check
			}
		}

		/// <summary>
		/// Calculates the next available time after a conflict, ensuring consistent gaps between tasks.
		/// Finds the latest end time of conflicting tasks, adds buffer, and rounds up to the next increment.
		/// </summary>
		private async Task<TimeSpan?> CalculateNextAvailableTimeAsync(DateTime date, TimeSpan candidateTime, int durationMinutes, Guid userId, int bufferMinutes, int incrementMinutes)
		{
			try
			{
				// Treat input as UTC for comparisons
				var candidateStart = DateTime.SpecifyKind(date.Date.Add(candidateTime), DateTimeKind.Utc);
				var candidateEnd = candidateStart.AddMinutes(durationMinutes);

				// Get all tasks on the same date
				var tasksOnDate = await _db.Tasks
					.Where(t => t.UserId == userId 
						&& t.IsActive 
						&& (t.Date.Date == candidateStart.Date || t.Time.Date == candidateStart.Date))
					.ToListAsync();

				if (tasksOnDate.Count == 0)
				{
					// No tasks on this date, return null to use fallback
					return null;
				}

				// Find the latest end time of tasks that conflict with the candidate time
				DateTime? latestConflictEnd = null;
				var bufferMinutesLocal = bufferMinutes;

				foreach (var existingTask in tasksOnDate)
				{
					var existingTaskStart = existingTask.Time;
					if (existingTaskStart.Kind != DateTimeKind.Utc)
					{
						existingTaskStart = DateTime.SpecifyKind(existingTaskStart, DateTimeKind.Utc);
					}
					var existingTaskEnd = existingTaskStart.AddMinutes(existingTask.DurationMinutes);

					// Check if this task conflicts with the candidate (with buffer)
					var candidateStartWithBuffer = candidateStart.AddMinutes(-bufferMinutesLocal);
					var candidateEndWithBuffer = candidateEnd.AddMinutes(bufferMinutesLocal);
					var existingTaskStartWithBuffer = existingTaskStart.AddMinutes(-bufferMinutesLocal);
					var existingTaskEndWithBuffer = existingTaskEnd.AddMinutes(bufferMinutesLocal);

					bool hasOverlap = (candidateStartWithBuffer < existingTaskEndWithBuffer) && (candidateEndWithBuffer > existingTaskStartWithBuffer);

					if (hasOverlap)
					{
						// This task conflicts - track its end time (with buffer)
						var conflictEnd = existingTaskEndWithBuffer;
						if (!latestConflictEnd.HasValue || conflictEnd > latestConflictEnd.Value)
						{
							latestConflictEnd = conflictEnd;
						}
					}
				}

				if (!latestConflictEnd.HasValue)
				{
					// No conflicts found (shouldn't happen if this method is called), return null
					return null;
				}

				// Calculate next available time: latest conflict end, rounded up to next increment
				var nextAvailableDateTime = latestConflictEnd.Value;
				var nextAvailableTimeOfDay = nextAvailableDateTime.TimeOfDay;

				// Round up to the next increment (30-minute mark)
				var totalMinutes = (int)nextAvailableTimeOfDay.TotalMinutes;
				var remainder = totalMinutes % incrementMinutes;
				if (remainder > 0)
				{
					totalMinutes = totalMinutes + (incrementMinutes - remainder);
				}

				// Ensure we're advancing forward from the candidate time
				var candidateTotalMinutes = (int)candidateTime.TotalMinutes;
				if (totalMinutes <= candidateTotalMinutes)
				{
					// The calculated time is not forward - advance by at least one increment
					totalMinutes = candidateTotalMinutes + incrementMinutes;
					// Round to next increment
					remainder = totalMinutes % incrementMinutes;
					if (remainder > 0)
					{
						totalMinutes = totalMinutes + (incrementMinutes - remainder);
					}
				}

				// Ensure we don't exceed 24 hours
				if (totalMinutes >= 24 * 60)
				{
					return null; // Will trigger day rollover in calling code
				}

				var nextAvailableTime = TimeSpan.FromMinutes(totalMinutes);

				_logger.LogDebug("Calculated next available time: {NextTime} (after conflict ending at {ConflictEnd}, candidate was {CandidateTime})", 
					nextAvailableTime, latestConflictEnd.Value, candidateTime);

				return nextAvailableTime;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating next available time for {Date} at {Time}", date, candidateTime);
				return null; // Fallback to simple increment
			}
		}

	private async Task<(DateTime date, TimeSpan time)> FindAvailableTimeInDayAsync(DateTime date, (TimeSpan start, TimeSpan end) slots, int durationMinutes, Guid userId)
	{
    // Handle windows that may cross midnight (e.g., 22:00 -> 01:00)
    var start = slots.start;
    var end = slots.end;

    // Local function to scan a single segment [segStart, segEnd) on a specific date
    async Task<(DateTime scanDate, TimeSpan time)> ScanAsync(TimeSpan segStart, TimeSpan segEnd, DateTime scanDate)
    {
        _logger?.LogInformation("[SCAN] Starting scan - segStart: {SegStart}, segEnd: {SegEnd}, scanDate: {ScanDate}, duration: {Duration}min", 
            segStart, segEnd, scanDate.ToString("yyyy-MM-dd"), durationMinutes);
        var t = segStart;
        // For segments that don't cross midnight, ensure we don't exceed segEnd
        var maxTime = segEnd;
        if (segEnd < segStart)
        {
            // Segment crosses midnight, use 24:00 as max for the first part
            maxTime = new TimeSpan(24, 0, 0);
        }
        
        int attemptCount = 0;
        while (t.Add(TimeSpan.FromMinutes(durationMinutes)) <= maxTime)
        {
            attemptCount++;
            var isAvailable = await IsTimeSlotAvailableAsync(scanDate, t, durationMinutes, userId);
            _logger?.LogInformation("[SCAN] Attempt {Attempt}: Checking {Time} on {Date} - Available: {Available}", 
                attemptCount, t, scanDate.ToString("yyyy-MM-dd"), isAvailable);
            
            if (isAvailable)
            {
                _logger?.LogInformation("[SCAN] Found available time: {Time} on {Date}", t, scanDate.ToString("yyyy-MM-dd"));
                return (scanDate, t);
            }
            t = t.Add(TimeSpan.FromMinutes(30));
            
            // Prevent infinite loop
            if (t.TotalMinutes >= 24 * 60)
            {
                _logger?.LogWarning("[SCAN] Time exceeded 24 hours, breaking loop");
                break;
            }
        }
        _logger?.LogInformation("[SCAN] No available time found after {AttemptCount} attempts", attemptCount);
        return (DateTime.MinValue, TimeSpan.Zero);
    }

    // Case 1: simple same-day window (e.g., 12:00 to 21:00)
    if (end > start)
    {
        var found = await ScanAsync(start, end, date);
        // Check if a time was found by checking the date (ScanAsync returns DateTime.MinValue when no time is found)
        if (found.scanDate != DateTime.MinValue)
            return (found.scanDate, found.time);
        return (DateTime.MinValue, TimeSpan.Zero);
    }

    // Case 2: window crosses midnight (e.g., 18:00 to 03:00)
    // For a slot that crosses midnight, when checking a specific date, we need to check BOTH portions:
    // 1. The before-midnight portion (start to 24:00) - the portion that STARTS on the current date
    //    Example: On date X, check 18:00-24:00 on date X (this is date X's slot starting)
    // 2. The after-midnight portion (00:00 to end) - the portion that CONTINUES into the next day
    //    Example: On date X, check 00:00-03:00 on date X+1 (this is date X's slot continuing into next day)
    //
    // Why check both? Because when checking date X with slot 18:00-03:00:
    // - Date X's slot: 18:00 (X) to 03:00 (X+1) - spans two days
    // So on date X, we need to check:
    // - 18:00-24:00 on date X (the portion that starts on date X)
    // - 00:00-03:00 on date X+1 (the portion that continues into the next day)
    
    // First, check the before-midnight portion (start to 24:00) on the current date
    // This is the portion of the slot that starts on the current date
    _logger?.LogInformation("[FIND_AVAILABLE] Checking before-midnight portion: {Start} to 24:00 on {Date} (slot crosses midnight, ends at {End} next day)", 
        start, date.ToString("yyyy-MM-dd"), end);
    var beforeMidnight = await ScanAsync(start, new TimeSpan(24, 0, 0), date);
    if (beforeMidnight.scanDate != DateTime.MinValue)
    {
        _logger?.LogInformation("[FIND_AVAILABLE] Found time in before-midnight portion: {Time} on {Date}", beforeMidnight.time, beforeMidnight.scanDate.ToString("yyyy-MM-dd"));
        return (beforeMidnight.scanDate, beforeMidnight.time);
    }

    // Then, check the after-midnight portion (00:00 to end) on the NEXT day
    // This is the portion of the slot that continues into the next day
    // For slot 18:00-03:00: When checking date X, the after-midnight portion (00:00-03:00) 
    // belongs to date X+1, not date X
    // Skip if end is 00:00 (TimeSpan.Zero) because that means the slot ends exactly at midnight
    if (end != TimeSpan.Zero)
    {
        var nextDate = date.AddDays(1);
        _logger?.LogInformation("[FIND_AVAILABLE] Checking after-midnight portion: 00:00 to {End} on {NextDate} (continuation of current day's slot into next day)", 
            end, nextDate.ToString("yyyy-MM-dd"));
        var afterMidnight = await ScanAsync(TimeSpan.Zero, end, nextDate);
        if (afterMidnight.scanDate != DateTime.MinValue)
        {
            _logger?.LogInformation("[FIND_AVAILABLE] Found time in after-midnight portion: {Time} on {Date}", afterMidnight.time, afterMidnight.scanDate.ToString("yyyy-MM-dd"));
            return (afterMidnight.scanDate, afterMidnight.time);
        }
    }

    // No time found in either portion
    return (DateTime.MinValue, TimeSpan.Zero);
	}

	private async Task<(DateTime date, TimeSpan time)> FindNextAvailableDateForTimeAsync(TimeSpan preferredTime, int durationMinutes, (TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots, Guid userId)
	{
		// Use UTC date explicitly
		var startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);
		var maxDays = 14;

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = DateTime.SpecifyKind(startDate.AddDays(dayOffset).Date, DateTimeKind.Utc);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? weekendSlots : weekdaySlots;

			// Check if preferred time fits within available slots
			// Handle both normal slots and midnight-crossing slots
			bool timeFitsInSlot;
			if (slots.end > slots.start)
			{
				// Normal slot: start < end (e.g., 12:00 to 21:00)
				timeFitsInSlot = preferredTime >= slots.start && preferredTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end;
			}
			else
			{
				// Slot crosses midnight: start > end (e.g., 18:00 to 03:00)
				// Time fits if it's >= start OR < end (with duration check)
				timeFitsInSlot = (preferredTime >= slots.start && preferredTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= new TimeSpan(24, 0, 0)) ||
								  (preferredTime < slots.end && preferredTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= slots.end);
			}
			
			if (timeFitsInSlot)
			{
				// Check if this time slot is available
				if (await IsTimeSlotAvailableAsync(date, preferredTime, durationMinutes, userId))
				{
					return (date, preferredTime);
				}
			}
		}

		return (DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), TimeSpan.Zero);
	}

	private async Task<(DateTime date, TimeSpan time)> FindNextAvailableSlotAsync(int durationMinutes, (TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots, Guid userId, DTOs.WellnessCheckInDto? wellnessData = null)
	{
		// Use UTC date explicitly - Start from tomorrow (UTC) to ensure we have a full day
		// For midnight-crossing slots, when checking tomorrow, we'll check its after-midnight portion (00:00-03:00)
		// which is the continuation of today's slot, and then the before-midnight portion (18:00-24:00)
		var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
		var startDate = today.AddDays(1); // Start from tomorrow to ensure full day availability
		var maxDays = 14; // Look ahead 2 weeks

		_logger?.LogInformation("[FIND_NEXT_SLOT] Starting search from {StartDate} (today: {Today})", startDate.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = DateTime.SpecifyKind(startDate.AddDays(dayOffset).Date, DateTimeKind.Utc);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			
			_logger?.LogInformation("[FIND_NEXT_SLOT] Checking date {Date} (dayOffset: {Offset}, isWeekend: {IsWeekend})", date.ToString("yyyy-MM-dd"), dayOffset, isWeekend);
			
			// Use UTC fields if available (they're already converted to UTC)
			// Otherwise use the slots passed in or fall back to converting local time fields
			(TimeSpan start, TimeSpan end) slots;
			if (wellnessData != null)
			{
				if (isWeekend && wellnessData.WeekendStartTimeUtc.HasValue && wellnessData.WeekendEndTimeUtc.HasValue)
				{
					// Use UTC fields directly - extract time portion
					slots = (wellnessData.WeekendStartTimeUtc.Value.TimeOfDay, wellnessData.WeekendEndTimeUtc.Value.TimeOfDay);
				}
				else if (!isWeekend && wellnessData.WeekdayStartTimeUtc.HasValue && wellnessData.WeekdayEndTimeUtc.HasValue)
				{
					// Use UTC fields directly - extract time portion
					slots = (wellnessData.WeekdayStartTimeUtc.Value.TimeOfDay, wellnessData.WeekdayEndTimeUtc.Value.TimeOfDay);
				}
				else if (!string.IsNullOrWhiteSpace(wellnessData.TimezoneId))
				{
					// Fall back to converting local time fields
					if (isWeekend)
					{
						slots = ParseTimeSlotsForDate(
							wellnessData.WeekendStartTime,
							wellnessData.WeekendStartShift,
							wellnessData.WeekendEndTime,
							wellnessData.WeekendEndShift,
							date,
							wellnessData.TimezoneId);
					}
					else
					{
						slots = ParseTimeSlotsForDate(
							wellnessData.WeekdayStartTime,
							wellnessData.WeekdayStartShift,
							wellnessData.WeekdayEndTime,
							wellnessData.WeekdayEndShift,
							date,
							wellnessData.TimezoneId);
					}
				}
				else
				{
					// No timezone, use passed-in slots
					slots = isWeekend ? weekendSlots : weekdaySlots;
				}
			}
			else
			{
				slots = isWeekend ? weekendSlots : weekdaySlots;
			}

			// Find available time within the day's slots
			var (availableDate, availableTime) = await FindAvailableTimeInDayAsync(date, slots, durationMinutes, userId);
			// Check if a time was found by checking the date (FindAvailableTimeInDayAsync returns DateTime.MinValue when no time is found)
			if (availableDate != DateTime.MinValue)
			{
				return (availableDate, availableTime);
			}
		}

		// Fallback: return tomorrow at start of available slots (UTC)
		var fallbackDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);
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
		
		// Handle specific times like "9:00 AM", "2:30 PM" - Convert US Eastern to UTC
		if (suggestedTime.Contains("AM") || suggestedTime.Contains("PM"))
		{
			// Extract time and AM/PM parts
			var timePart = suggestedTime.Replace("AM", "").Replace("PM", "").Trim();
			var isPM = suggestedTime.ToUpper().Contains("PM");
			
			if (TimeSpan.TryParse(timePart, out var easternTime))
			{
				// Convert AM/PM to 24-hour format
				if (isPM && easternTime.Hours >= 1 && easternTime.Hours <= 12)
				{
					easternTime = easternTime.Add(new TimeSpan(12, 0, 0));
				}
				else if (!isPM && easternTime.Hours == 12)
				{
					easternTime = easternTime.Subtract(new TimeSpan(12, 0, 0));
				}
				
				// Convert US Eastern to UTC
				var offset1 = TimeSpan.FromHours(5); // UTC-5 for US Eastern Standard Time
				return ConvertEasternToUtc(easternTime, offset1);
			}
		}
		
		// Handle 24-hour format times like "09:00", "17:00" - Convert US Eastern to UTC
		if (TimeSpan.TryParse(suggestedTime, out var easternTime24))
		{
			// Convert US Eastern to UTC
			var offset2 = TimeSpan.FromHours(5); // UTC-5 for US Eastern Standard Time
			return ConvertEasternToUtc(easternTime24, offset2);
		}
		
		// Handle time periods - Convert US Eastern times to UTC
		var timeLower = suggestedTime.ToLower();
		var utcOffset = TimeSpan.FromHours(5); // UTC-5 for US Eastern Standard Time
		
		return timeLower switch
		{
			"morning" => ConvertEasternToUtc(new TimeSpan(9, 0, 0), utcOffset),   // 9:00 AM US Eastern = 2:00 PM UTC
			"afternoon" => ConvertEasternToUtc(new TimeSpan(14, 0, 0), utcOffset), // 2:00 PM US Eastern = 7:00 PM UTC
			"evening" => ConvertEasternToUtc(new TimeSpan(18, 0, 0), utcOffset),     // 6:00 PM US Eastern = 11:00 PM UTC
			"weekend" => ConvertEasternToUtc(new TimeSpan(10, 0, 0), utcOffset),    // 10:00 AM US Eastern = 3:00 PM UTC
			"weekday" => ConvertEasternToUtc(new TimeSpan(9, 0, 0), utcOffset),     // 9:00 AM US Eastern = 2:00 PM UTC
			_ => null // Unknown format, let system decide
		};
	}

	private static (TimeSpan start, TimeSpan end) ParseTimeSlots(string? startTime, string? endTime, string? startShift, string? endShift)
	{
		// Add debugging - we'll use Console.WriteLine since this is a static method
		Console.WriteLine($"[DEBUG] ParseTimeSlots called with: startTime='{startTime}', endTime='{endTime}', startShift='{startShift}', endShift='{endShift}'");
		
		if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
		{
			Console.WriteLine("[DEBUG] Missing time data, using default slots: 7 PM to 10 PM UTC");
			// Default: 7 PM to 10 PM UTC (frontend will convert to user's local timezone)
			return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0)); // Default 7 PM to 10 PM UTC
		}

		// Parse times - handle both ISO 8601 format (from UTC fields) and simple time format
		// UTC fields are in ISO 8601 format: "2025-11-17T19:00:00Z"
		// Original fields are in simple format: "19:00" with optional shift
		var startUtc = ParseTimeString(startTime, startShift);
		var endUtc = ParseTimeString(endTime, endShift);
		
		Console.WriteLine($"[DEBUG] Parsed UTC time slots - Start: {startUtc}, End: {endUtc}");
		
		// Validate that start is before end (handle day boundary crossing)
		if (startUtc >= endUtc)
		{
			// Check if this is a day boundary crossing (e.g., 22:00 to 01:00 next day)
			// This is valid for time slots that span midnight
			var isDayBoundaryCrossing = startUtc.TotalMinutes > endUtc.TotalMinutes && 
				(startUtc.TotalMinutes >= 22 * 60 || endUtc.TotalMinutes <= 2 * 60); // After 10 PM or before 2 AM
			
			Console.WriteLine($"[DEBUG] Day boundary check - isDayBoundaryCrossing: {isDayBoundaryCrossing}");
			
			if (!isDayBoundaryCrossing)
			{
				Console.WriteLine("[DEBUG] Invalid time slots, using defaults");
				return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0)); // Default 7 PM to 10 PM UTC
			}
		}
		
		Console.WriteLine($"[DEBUG] Final UTC time slots: {startUtc} to {endUtc}");
		return (startUtc, endUtc);
	}

	// Overload for DateTime? parameters (from UTC fields) - DEPRECATED: Use ParseTimeSlotsForDate instead
	private static (TimeSpan start, TimeSpan end) ParseTimeSlots(DateTime? startTime, DateTime? endTime)
	{
		if (!startTime.HasValue || !endTime.HasValue)
		{
			Console.WriteLine("[DEBUG] Missing DateTime time data, using default slots: 7 PM to 10 PM UTC");
			return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0)); // Default 7 PM to 10 PM UTC
		}

		// Extract time portion from DateTime objects
		var startUtc = startTime.Value.TimeOfDay;
		var endUtc = endTime.Value.TimeOfDay;
		
		Console.WriteLine($"[DEBUG] Parsed UTC time slots from DateTime - Start: {startUtc}, End: {endUtc}");
		
		// Validate that start is before end (handle day boundary crossing)
		if (startUtc >= endUtc)
		{
			// Check if this is a day boundary crossing (e.g., 22:00 to 01:00 next day)
			var isDayBoundaryCrossing = startUtc.TotalMinutes > endUtc.TotalMinutes && 
				(startUtc.TotalMinutes >= 22 * 60 || endUtc.TotalMinutes <= 2 * 60);
			
			Console.WriteLine($"[DEBUG] Day boundary check - isDayBoundaryCrossing: {isDayBoundaryCrossing}");
			
			if (!isDayBoundaryCrossing)
			{
				Console.WriteLine("[DEBUG] Invalid time slots, using defaults");
				return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0)); // Default 7 PM to 10 PM UTC
			}
		}
		
		Console.WriteLine($"[DEBUG] Final UTC time slots: {startUtc} to {endUtc}");
		return (startUtc, endUtc);
	}

	/// <summary>
	/// Parses time slots from local time strings and converts them to UTC TimeSpan for a specific target date.
	/// This ensures correct timezone conversion for the target scheduling date.
	/// </summary>
	private static (TimeSpan start, TimeSpan end) ParseTimeSlotsForDate(
		string? startTime, 
		string? startShift, 
		string? endTime, 
		string? endShift, 
		DateTime targetDate,
		string? timezoneId)
	{
		if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
		{
			Console.WriteLine("[DEBUG] Missing time data, using default slots: 7 PM to 10 PM UTC");
			return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0));
		}

		// Parse local time strings to TimeSpan
		var localStartTime = ParseTimeString(startTime, startShift);
		var localEndTime = ParseTimeString(endTime, endShift);

		// Convert local time to UTC for the target date
		var startUtc = ConvertLocalTimeToUtcForDate(localStartTime, targetDate, timezoneId);
		var endUtc = ConvertLocalTimeToUtcForDate(localEndTime, targetDate, timezoneId);

		Console.WriteLine($"[DEBUG] Parsed time slots for date {targetDate:yyyy-MM-dd}: Local {localStartTime}-{localEndTime} -> UTC {startUtc}-{endUtc}");

		// Handle day boundary crossing (e.g., 10 PM to 1 AM next day)
		if (startUtc >= endUtc)
		{
			// If end time is earlier than start time, it might cross midnight
			// Check if this is a valid day boundary crossing
			var isDayBoundaryCrossing = (localStartTime.TotalMinutes >= 22 * 60 || localEndTime.TotalMinutes <= 2 * 60);
			
			if (!isDayBoundaryCrossing)
			{
				Console.WriteLine("[DEBUG] Invalid time slots, using defaults");
				return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0));
			}
		}

		Console.WriteLine($"[DEBUG] Final UTC time slots for {targetDate:yyyy-MM-dd}: {startUtc} to {endUtc}");
		return (startUtc, endUtc);
	}

	/// <summary>
	/// Converts a local time (TimeSpan) to UTC TimeSpan for a specific date using the timezone ID.
	/// </summary>
	private static TimeSpan ConvertLocalTimeToUtcForDate(TimeSpan localTime, DateTime targetDate, string? timezoneId)
	{
		if (string.IsNullOrWhiteSpace(timezoneId))
		{
			// No timezone provided, assume time is already in UTC
			return localTime;
		}

		try
		{
			// Get the timezone info
			TimeZoneInfo timeZone;
			try
			{
				timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
			}
			catch (TimeZoneNotFoundException)
			{
				// Map common IANA IDs to Windows IDs
				var windowsId = timezoneId switch
				{
					"America/Chicago" => "Central Standard Time",
					"America/New_York" => "Eastern Standard Time",
					"America/Denver" => "Mountain Standard Time",
					"America/Los_Angeles" => "Pacific Standard Time",
					"America/Phoenix" => "US Mountain Standard Time",
					_ => timezoneId
				};
				timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
			}

			// targetDate is a UTC date representing the target day (e.g., 2025-11-20 00:00:00 UTC)
			// We need to interpret the DATE part as a LOCAL date in the user's timezone
			// Extract just the date part (year, month, day) and treat it as local
			var year = targetDate.Year;
			var month = targetDate.Month;
			var day = targetDate.Day;
			
			// Create a DateTime in the user's local timezone using the date components + local time
			// This represents the local date/time (e.g., Nov 20, 2025 7:00 PM CST)
			var localDateTime = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified).Add(localTime);
			
			// Convert to UTC using the timezone
			var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
			utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

			// Return the time portion (TimeSpan) - this is the UTC time
			return utcDateTime.TimeOfDay;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[DEBUG] Failed to convert time using timezone {timezoneId}, assuming time is already in UTC: {ex.Message}");
			// Fallback: assume time is already in UTC
			return localTime;
		}
	}

	/// <summary>
	/// Converts US Eastern Time to UTC by adding the UTC offset.
	/// </summary>
	private static TimeSpan ConvertEasternToUtc(TimeSpan easternTime, TimeSpan utcOffset)
	{
		var utcTime = easternTime.Add(utcOffset);
		
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

			var freq = frequency.Trim().ToLowerInvariant();
			return freq switch
			{
				"once" => RepeatType.Never,
				"never" => RepeatType.Never,
				"daily" => RepeatType.Day,
				"weekdays" => RepeatType.Day, // Treat weekday blocks as daily for now
				"weekly" => RepeatType.Week,
				"bi-weekly" or "biweekly" => RepeatType.Week,
				"monthly" => RepeatType.Month,
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
			return new TimeSpan(14, 0, 0); // Default 9 AM US Eastern = 2 PM UTC

		var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
		
		if (isWeekend)
		{
			// Convert local time to UTC for the target date
			var weekendSlots = ParseTimeSlotsForDate(
				wellnessData.WeekendStartTime, 
				wellnessData.WeekendStartShift,
				wellnessData.WeekendEndTime,
				wellnessData.WeekendEndShift,
				date,
				wellnessData.TimezoneId);
			return GetOptimalTimeInRange(weekendSlots.start, weekendSlots.end);
		}
		else
		{
			// Convert local time to UTC for the target date
			var weekdaySlots = ParseTimeSlotsForDate(
				wellnessData.WeekdayStartTime,
				wellnessData.WeekdayStartShift,
				wellnessData.WeekdayEndTime,
				wellnessData.WeekdayEndShift,
				date,
				wellnessData.TimezoneId);
			return GetOptimalTimeInRange(weekdaySlots.start, weekdaySlots.end);
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
				
				(TimeSpan startTime, TimeSpan endTime) slots;
				if (isWeekend)
				{
					// Use UTC fields (DateTime objects)
					slots = ParseTimeSlots(wellnessData.WeekendStartTimeUtc, wellnessData.WeekendEndTimeUtc);
				}
				else
				{
					// Use UTC fields (DateTime objects)
					slots = ParseTimeSlots(wellnessData.WeekdayStartTimeUtc, wellnessData.WeekdayEndTimeUtc);
				}

				// Check if preferred time falls within available range
				if (preferredTime >= slots.startTime && preferredTime <= slots.endTime)
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
			return (DateTime.UtcNow.Date.AddDays(1), new TimeSpan(14, 0, 0)); // Default 9 AM US Eastern = 2 PM UTC

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

		// Check if timeStr is actually a DateTime (when passed from UTC fields)
		// This handles the case where UTC fields are DateTime objects converted to string
		if (timeStr != null && DateTime.TryParse(timeStr, out var isoDateTime))
		{
			// Extract time portion from datetime
			return isoDateTime.TimeOfDay;
		}

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

        /// <summary>
        /// Normalizes urgency/importance strings to \"Low\" | \"Medium\" | \"High\".
        /// </summary>
        private static string NormalizePriorityLevel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Medium";

            var v = value.Trim().ToLowerInvariant();
            if (v.StartsWith("high")) return "High";
            if (v.StartsWith("med")) return "Medium";
            if (v.StartsWith("low")) return "Low";

            // Fallback: map common variants
            if (v.Contains("urgent")) return "High";
            if (v.Contains("soon")) return "Medium";

            return "Medium";
        }

        /// <summary>
        /// Maps \"Low\" | \"Medium\" | \"High\" to numeric score 1, 3, 5.
        /// </summary>
        private static int MapLevelToScore(string? level)
        {
            switch (level)
            {
                case "High":
                    return 5;
                case "Low":
                    return 1;
                default:
                    return 3;
            }
        }

        private async Task<List<string>> ExtractEmotionsAsync(string text)
        {
			var prompt = $@"
				[INST]
				You are a JSON extraction tool. Extract the top 3 emotions from the user's text.
				
				CRITICAL: Return ONLY a valid JSON array. Do NOT include any explanations, descriptions, or additional text.
				Do NOT use phrases like ""Sure, here are..."", ""The emotions are:"", or any other introductory text.
				Do NOT provide explanations for why each emotion was chosen.
				
				Format: [""emotion1"", ""emotion2"", ""emotion3""]
				Example: [""happy"", ""anxious"", ""frustrated""]
				
				Text:
				{text}
				
				Response (JSON array only, no other text):
				[/INST]";

            var response = await _runPodService.SendPromptAsync(prompt, 200, 0.2);

            try
            {
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(response);
                
                // Try to extract JSON array from the response text (may contain explanatory text)
                var jsonStart = extractedText.IndexOf('[');
                var jsonEnd = extractedText.LastIndexOf(']');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonArray = extractedText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<List<string>>(jsonArray) ?? new List<string>();
                }
                
                // Fallback: try to deserialize the entire extracted text
                return JsonSerializer.Deserialize<List<string>>(extractedText) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        private async Task<string> SummarizeMindDumpAsync(string text)
        {
            var prompt = $@"
			[INST]
			Summarize the user's brain dump in 3–4 clear sentences.
			Do not add advice. Preserve all actionable themes.

			Text:
			{text}
			[/INST]";

            var response = await _runPodService.SendPromptAsync(prompt, 300, 0.3);
            
            // Extract text from RunPod response envelope (handles both new and old structures)
            return RunpodResponseHelper.ExtractTextFromRunpodResponse(response);
        }
        private async Task<List<string>> ExtractTopicsAsync(string text)
        {
            var prompt = $@"
				[INST]
				You are a JSON extraction tool. Extract all major topics from the text.
				Topics should be simple words like: moving, work, kids, health, chores, relationships, school.
				
				CRITICAL: Return ONLY a valid JSON array. Do NOT include any explanations, descriptions, or additional text.
				Do NOT use phrases like ""Sure, here are..."", ""The topics are:"", or any other introductory text.
				Do NOT provide explanations for why each topic was chosen.
				
				Format: [""topic1"", ""topic2"", ""topic3""]
				Example: [""work"", ""family"", ""health""]
				
				Text:
				{text}
				
				Response (JSON array only, no other text):
				[/INST]";

            var response = await _runPodService.SendPromptAsync(prompt, 200, 0.2);

            try
            {
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(response);
                
                // Try to extract JSON array from the response text (may contain explanatory text)
                var jsonStart = extractedText.IndexOf('[');
                var jsonEnd = extractedText.LastIndexOf(']');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonArray = extractedText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<List<string>>(jsonArray) ?? new();
                }
                
                // Fallback: try to deserialize the entire extracted text
                return JsonSerializer.Deserialize<List<string>>(extractedText) ?? new();
            }
            catch
            {
                return new();
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

		private async Task<ProgressMetricsDto?> CalculateProgressMetricsAsync(Guid userId)
		{
			try
			{
				_logger.LogDebug("Calculating progress metrics for user {UserId}", userId);
				
				var now = DateTime.UtcNow;
				// Calculate start of this week (Sunday = 0, so we need to adjust)
				var daysSinceSunday = (int)now.DayOfWeek;
				var thisWeekStart = now.Date.AddDays(-daysSinceSunday);
				var lastWeekStart = thisWeekStart.AddDays(-7);
				
				_logger.LogDebug("Week calculation: Now={Now}, DaysSinceSunday={DaysSinceSunday}, ThisWeekStart={ThisWeekStart}, LastWeekStart={LastWeekStart}", 
					now, daysSinceSunday, thisWeekStart, lastWeekStart);

				// Get brain dump entries for this week and last week
				var thisWeekEntries = await _db.BrainDumpEntries
					.Where(e => e.UserId == userId 
						&& e.CreatedAtUtc >= thisWeekStart 
						&& e.DeletedAtUtc == null)
					.ToListAsync();

				var lastWeekEntries = await _db.BrainDumpEntries
					.Where(e => e.UserId == userId 
						&& e.CreatedAtUtc >= lastWeekStart 
						&& e.CreatedAtUtc < thisWeekStart
						&& e.DeletedAtUtc == null)
					.ToListAsync();

				// Calculate brain dump frequency
				var thisWeekCount = thisWeekEntries.Count;
				var lastWeekCount = lastWeekEntries.Count;
				var frequencyChange = thisWeekCount - lastWeekCount;

				// Calculate average mood and stress scores
				var thisWeekMoodScores = thisWeekEntries.Where(e => e.Mood.HasValue).Select(e => (double)e.Mood!.Value).ToList();
				var thisWeekStressScores = thisWeekEntries.Where(e => e.Stress.HasValue).Select(e => (double)e.Stress!.Value).ToList();
				
				var lastWeekMoodScores = lastWeekEntries.Where(e => e.Mood.HasValue).Select(e => (double)e.Mood!.Value).ToList();
				var lastWeekStressScores = lastWeekEntries.Where(e => e.Stress.HasValue).Select(e => (double)e.Stress!.Value).ToList();

				var avgMoodThisWeek = thisWeekMoodScores.Any() ? thisWeekMoodScores.Average() : 0;
				var avgStressThisWeek = thisWeekStressScores.Any() ? thisWeekStressScores.Average() : 0;
				var avgMoodLastWeek = lastWeekMoodScores.Any() ? lastWeekMoodScores.Average() : 0;
				var avgStressLastWeek = lastWeekStressScores.Any() ? lastWeekStressScores.Average() : 0;

				var moodChange = avgMoodLastWeek > 0 ? avgMoodThisWeek - avgMoodLastWeek : 0;
				var stressChange = avgStressLastWeek > 0 ? avgStressThisWeek - avgStressLastWeek : 0;

				// Calculate task completion rate
				var totalTasks = await _db.Tasks
					.Where(t => t.UserId == userId && t.IsActive)
					.CountAsync();

				var completedTasks = await _db.Tasks
					.Where(t => t.UserId == userId && t.IsActive && t.Status == Models.TaskStatus.Completed)
					.CountAsync();

				var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;

				// Generate interpretation
				var interpretation = BuildProgressInterpretation(avgMoodThisWeek, moodChange, avgStressThisWeek, stressChange, completionRate, frequencyChange);

				_logger.LogDebug("Calculated progress metrics for user {UserId}. CompletionRate: {CompletionRate}%, BrainDumpFrequency: {Frequency}, MoodChange: {MoodChange}", 
					userId, completionRate, thisWeekCount, moodChange);

				return new ProgressMetricsDto(
					completionRate,
					thisWeekCount,
					frequencyChange,
					avgMoodThisWeek,
					moodChange,
					avgStressThisWeek,
					stressChange,
					interpretation
				);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to calculate progress metrics for user {UserId}", userId);
				// Return default metrics instead of null
				return new ProgressMetricsDto(
					0.0,
					0,
					0,
					0.0,
					0.0,
					0.0,
					0.0,
					"Start tracking your wellness to see progress metrics here!"
				);
			}
		}

        private async Task<EmotionTrendsDto?> AnalyzeEmotionTrendsAsync(
        Guid userId,
        BrainDumpEntry? currentEntry,
        List<string> extractedEmotions)
        {
            try
            {
                _logger.LogDebug("Analyzing emotion trends for user {UserId}", userId);

                var now = DateTime.UtcNow;
                var daysSinceSunday = (int)now.DayOfWeek;
                var thisWeekStart = now.Date.AddDays(-daysSinceSunday);

                var entries = await _db.BrainDumpEntries
                    .Where(e => e.UserId == userId
                        && e.CreatedAtUtc >= thisWeekStart
                        && e.DeletedAtUtc == null)
                    .ToListAsync();

                if (currentEntry != null && !entries.Any(e => e.Id == currentEntry.Id))
                {
                    entries.Add(currentEntry);
                }

                if (entries.Count == 0)
                {
                    return new EmotionTrendsDto(
                        new Dictionary<string, int>(),
                        extractedEmotions,
                        new List<string>()
                    );
                }

                // ------------------------------------------------------
                // ⭐ 1. Start with current extractedEmotions (highest confidence)
                // ------------------------------------------------------
                var emotionFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var emotion in extractedEmotions)
                {
                    emotionFrequency[emotion] = emotionFrequency.GetValueOrDefault(emotion, 0) + 1;
                }

                // ------------------------------------------------------
                // ⭐ 2. Add historical extracted emotions from stored entries
                // Note: Emotions are not stored directly in BrainDumpEntry.
                // Historical emotion analysis would require re-extracting from text or storing separately.
                // For now, we rely on the current extractedEmotions and keyword scanning below.
                // ------------------------------------------------------
                // TODO: If historical emotion storage is needed, consider adding an Emotions property to BrainDumpEntry
                // or extracting emotions from Tags if they contain emotion keywords

                // ------------------------------------------------------
                // ⭐ 3. Fallback: keyword scan only if no emotions found
                // ------------------------------------------------------
                if (emotionFrequency.Count == 0)
                {
                    _logger.LogDebug("No extracted emotions found, falling back to keyword scanning.");

                    var keywords = new[]
                    {
                "anxious","anxiety","worried","worry","stressed","stress","overwhelmed","overwhelm",
                "exhausted","exhaustion","tired","fatigue","burnout","burned out",
                "grateful","gratitude","thankful","appreciate","happy","happiness","joy","joyful",
                "sad","sadness","depressed","depression","down","low",
                "calm","peaceful","relaxed","content","satisfied",
                "frustrated","frustration","angry","anger","irritated","irritation"
            };

                    foreach (var entry in entries)
                    {
                        var text = $"{entry.Text} {entry.Context}".ToLower();

                        foreach (var keyword in keywords)
                        {
                            var count = CountOccurrences(text, keyword);
                            if (count > 0)
                            {
                                emotionFrequency[keyword] = emotionFrequency.GetValueOrDefault(keyword, 0) + count;
                            }
                        }
                    }
                }

                // ------------------------------------------------------
                // ⭐ 4. Determine top emotions
                // ------------------------------------------------------
                var topEmotions = emotionFrequency
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(3)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // ------------------------------------------------------
                // ⭐ 5. Build insights
                // ------------------------------------------------------
                var emotionInsights = new List<string>();

                foreach (var emotion in topEmotions)
                {
                    var count = emotionFrequency[emotion];

                    if (entries.Count == 1)
                        emotionInsights.Add($"You mentioned feeling '{emotion}' today.");
                    else
                        emotionInsights.Add($"You've experienced '{emotion}' {count} times this week.");
                }

                return new EmotionTrendsDto(
                    emotionFrequency,
                    topEmotions,
                    emotionInsights
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze emotion trends for user {UserId}", userId);

                return new EmotionTrendsDto(
                    new Dictionary<string, int>(),
                    new List<string>(),
                    new List<string>()
                );
            }
        }

        private int CountOccurrences(string text, string keyword)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
				return 0;

			var count = 0;
			var index = 0;
			while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
			{
				count++;
				index += keyword.Length;
			}
			return count;
		}

		private List<string> GenerateInsights(ProgressMetricsDto? progressMetrics, EmotionTrendsDto? emotionTrends, BrainDumpEntry? currentEntry = null)
		{
			var insights = new List<string>();

			if (progressMetrics != null)
			{
				// Task completion insight
				if (progressMetrics.TaskCompletionRate >= 80)
				{
					insights.Add($"You've completed {progressMetrics.TaskCompletionRate:F0}% of your suggested tasks - great progress!");
				}
				else if (progressMetrics.TaskCompletionRate >= 50)
				{
					insights.Add($"You've completed {progressMetrics.TaskCompletionRate:F0}% of your suggested tasks");
				}
				else if (progressMetrics.TaskCompletionRate > 0)
				{
					insights.Add($"You've completed {progressMetrics.TaskCompletionRate:F0}% of your suggested tasks - keep going!");
				}

				// Mood trend insight (lower threshold for visibility)
				if (progressMetrics.AverageMoodScoreChange > 0.5)
				{
					var oldMood = progressMetrics.AverageMoodScore - progressMetrics.AverageMoodScoreChange;
					insights.Add($"Your mood scores have improved from {oldMood:F1}/10 to {progressMetrics.AverageMoodScore:F1}/10");
				}
				else if (progressMetrics.AverageMoodScoreChange < -0.5)
				{
					var oldMood = progressMetrics.AverageMoodScore - progressMetrics.AverageMoodScoreChange;
					insights.Add($"Your mood scores have decreased from {oldMood:F1}/10 to {progressMetrics.AverageMoodScore:F1}/10");
				}
				else if (progressMetrics.AverageMoodScore > 0)
				{
					// Show current mood if available
					if (currentEntry != null && currentEntry.Mood.HasValue)
					{
						insights.Add($"Your current mood score is {currentEntry.Mood.Value}/10");
					}
					else
					{
						insights.Add($"Your average mood this week is {progressMetrics.AverageMoodScore:F1}/10");
					}
				}

				// Stress trend insight (lower threshold)
				if (progressMetrics.AverageStressScoreChange < -0.5)
				{
					var oldStress = progressMetrics.AverageStressScore - progressMetrics.AverageStressScoreChange;
					insights.Add($"Your stress mentions dropped {Math.Abs(progressMetrics.AverageStressScoreChange):F1} points this week");
				}
				else if (progressMetrics.AverageStressScoreChange > 0.5)
				{
					var oldStress = progressMetrics.AverageStressScore - progressMetrics.AverageStressScoreChange;
					insights.Add($"Your stress levels increased from {oldStress:F1}/10 to {progressMetrics.AverageStressScore:F1}/10");
				}
				else if (progressMetrics.AverageStressScore > 0)
				{
					// Show current stress if available
					if (currentEntry != null && currentEntry.Stress.HasValue)
					{
						insights.Add($"Your current stress level is {currentEntry.Stress.Value}/10");
					}
					else
					{
						insights.Add($"Your average stress this week is {progressMetrics.AverageStressScore:F1}/10");
					}
				}

				// Brain dump frequency insight
				if (progressMetrics.BrainDumpFrequencyChange > 0)
				{
					insights.Add($"You've been more consistent with brain dumps this week (+{progressMetrics.BrainDumpFrequencyChange} entries)");
				}
				else if (progressMetrics.BrainDumpFrequency == 1)
				{
					// For first-time users
					insights.Add("This is your first brain dump this week - great start on your wellness journey!");
				}
				else if (progressMetrics.BrainDumpFrequency > 1)
				{
					insights.Add($"You've completed {progressMetrics.BrainDumpFrequency} brain dumps this week - keep up the great work!");
				}
			}

			if (emotionTrends != null && emotionTrends.EmotionInsights.Any())
			{
				insights.AddRange(emotionTrends.EmotionInsights);
			}
			else if (currentEntry != null && emotionTrends != null && emotionTrends.TopEmotions.Any())
			{
				// Fallback: show top emotions even if they don't meet the threshold
				var topEmotion = emotionTrends.TopEmotions.FirstOrDefault();
				if (!string.IsNullOrEmpty(topEmotion))
				{
					var count = emotionTrends.EmotionFrequency.GetValueOrDefault(topEmotion, 0);
					if (count > 0)
					{
						insights.Add($"Your brain dump shows themes around '{topEmotion}'");
					}
				}
			}

			// Ensure we always have at least one insight for new users
			if (insights.Count == 0)
			{
				if (currentEntry != null)
				{
					insights.Add("Welcome to MindFlow! Your wellness journey starts here. Keep using brain dumps to see personalized insights.");
				}
				else
				{
					insights.Add("Start using brain dumps to track your wellness and see personalized insights here!");
				}
			}

			return insights;
		}

		private List<string> GeneratePatterns(EmotionTrendsDto? emotionTrends)
		{
			var patterns = new List<string>();

			if (emotionTrends != null && emotionTrends.TopEmotions.Any())
			{
				// Identify recurring patterns (lower threshold for new users)
				foreach (var emotion in emotionTrends.TopEmotions)
				{
					var count = emotionTrends.EmotionFrequency.GetValueOrDefault(emotion, 0);
					if (count >= 2)
					{
						patterns.Add($"You've mentioned '{emotion}' {count} times this week - this might be worth exploring");
					}
					else if (count >= 1)
					{
						// For single entries, still show pattern if it's a significant emotion
						var significantEmotions = new[] { "anxious", "anxiety", "overwhelmed", "exhausted", "exhaustion", "stressed", "stress" };
						if (significantEmotions.Contains(emotion.ToLower()))
						{
							patterns.Add($"You mentioned '{emotion}' in your brain dump - consider exploring this feeling further");
						}
					}
				}
			}

			return patterns;
		}

		private string BuildPersonalizedMessage(string userName, DTOs.WellnessCheckInDto? wellnessData, ProgressMetricsDto? progressMetrics, EmotionTrendsDto? emotionTrends)
		{
			var primaryFocus = "general wellness";
			var selfCareFrequency = "regular";
			
			var message = $"Hi {userName}, based on your focus on {primaryFocus} and {selfCareFrequency} self-care routine, we've tailored MindFlow AI to support your mental wellness journey.";

			if (progressMetrics != null && !string.IsNullOrWhiteSpace(progressMetrics.Interpretation))
			{
				message += $" {progressMetrics.Interpretation}";
			}

			return message;
		}

		private string BuildProgressInterpretation(double avgMood, double moodChange, double avgStress, double stressChange, double completionRate, int frequencyChange)
		{
			var interpretations = new List<string>();

			if (moodChange > 1)
			{
				interpretations.Add($"Your mood has improved significantly this week");
			}
			else if (moodChange < -1)
			{
				interpretations.Add($"Your mood has decreased this week - consider focusing on self-care");
			}

			if (stressChange < -1)
			{
				interpretations.Add($"Your stress levels have decreased this week");
			}
			else if (stressChange > 1)
			{
				interpretations.Add($"Your stress levels have increased this week");
			}

			if (completionRate >= 80)
			{
				interpretations.Add($"You're making excellent progress with task completion");
			}
			else if (completionRate < 50)
			{
				interpretations.Add($"Consider breaking tasks into smaller steps to improve completion");
			}

			if (frequencyChange > 0)
			{
				interpretations.Add($"You've been more consistent with brain dumps this week");
			}

			return interpretations.Any() ? string.Join(". ", interpretations) + "." : string.Empty;
		}

		public async Task<AnalyticsDto> GetAnalyticsAsync(Guid userId, Guid? brainDumpEntryId = null)
		{
			_logger.LogInformation("Getting analytics for user {UserId}", userId);

			try
			{
				// Get user name
				var userProfile = await _userService.GetProfileAsync(userId);
				var userName = GetUserDisplayName(userProfile);

				// Get wellness data
				var wellnessData = await _wellnessService.GetAsync(userId);

				// Get current entry if provided
				BrainDumpEntry? currentEntry = null;
				if (brainDumpEntryId.HasValue)
				{
					currentEntry = await _db.BrainDumpEntries
						.FirstOrDefaultAsync(e => e.Id == brainDumpEntryId.Value && e.UserId == userId && e.DeletedAtUtc == null);
				}

				// Gather analytics data - ensure we always get non-null values
				var progressMetrics = await CalculateProgressMetricsAsync(userId) ?? new ProgressMetricsDto(
					0.0, 0, 0, 0.0, 0.0, 0.0, 0.0, 
					"Start tracking your wellness to see progress metrics here!"
				);
				
				// Extract emotions from current entry if available, otherwise use empty list
				List<string> extractedEmotions = new();
				if (currentEntry != null && !string.IsNullOrWhiteSpace(currentEntry.Text))
				{
					try
					{
						extractedEmotions = await ExtractEmotionsAsync(currentEntry.Text);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to extract emotions from current entry for analytics");
					}
				}
				
				var emotionTrends = await AnalyzeEmotionTrendsAsync(userId, currentEntry, extractedEmotions) ?? new EmotionTrendsDto(
					new Dictionary<string, int>(),
					new List<string>(),
					new List<string>()
				);
				var insights = GenerateInsights(progressMetrics, emotionTrends, currentEntry) ?? new List<string> { "Start using MindFlow to see insights!" };
				var patterns = GeneratePatterns(emotionTrends) ?? new List<string>();

				// Create personalized message - ensure it's never null
				var personalizedMessage = BuildPersonalizedMessage(userName, wellnessData, progressMetrics, emotionTrends);
				if (string.IsNullOrWhiteSpace(personalizedMessage))
				{
					personalizedMessage = $"Hi {userName}, welcome to MindFlow AI! We're here to support your mental wellness journey.";
				}

				_logger.LogInformation("Successfully retrieved analytics for user {UserId}. Insights: {InsightsCount}, Patterns: {PatternsCount}",
					userId, insights?.Count ?? 0, patterns?.Count ?? 0);

				return new AnalyticsDto(
					insights,
					patterns,
					progressMetrics,
					emotionTrends,
					personalizedMessage
				);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while getting analytics for user {UserId}", userId);
				throw;
			}
		}

		/// <summary>
		/// Extracts a relevant text excerpt from the brain dump that relates to the task.
		/// </summary>
		private string? ExtractSourceTextExcerpt(string brainDumpText, string taskTitle, string? taskNotes)
		{
			if (string.IsNullOrWhiteSpace(brainDumpText))
				return null;

			var text = brainDumpText.ToLower();
			var taskLower = taskTitle.ToLower();
			var notesLower = taskNotes?.ToLower() ?? string.Empty;

			// Find keywords from task title in brain dump text
			var taskWords = taskLower.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
				.Where(w => w.Length > 3) // Only meaningful words
				.ToList();

			// Find sentences that contain task-related keywords
			var sentences = brainDumpText.Split(new[] { '.', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var relevantSentences = sentences
				.Where(s => taskWords.Any(w => s.ToLower().Contains(w)))
				.Take(2) // Take up to 2 relevant sentences
				.ToList();

			if (relevantSentences.Any())
			{
				var excerpt = string.Join(" ", relevantSentences).Trim();
				// Limit to 500 characters
				return excerpt.Length > 500 ? excerpt.Substring(0, 497) + "..." : excerpt;
			}

			// Fallback: return first 100 characters of brain dump
			return brainDumpText.Length > 100 ? brainDumpText.Substring(0, 97) + "..." : brainDumpText;
		}

		/// <summary>
		/// Determines the life area (Work, Family, Health, Relationships, Personal) from brain dump and task.
		/// </summary>
		private string? DetermineLifeArea(string brainDumpText, string taskTitle, string? taskNotes)
		{
			var text = $"{brainDumpText} {taskTitle} {taskNotes}".ToLower();

			// Work-related keywords
			if (text.Contains("work") || text.Contains("job") || text.Contains("career") || text.Contains("office") || 
			    text.Contains("deadline") || text.Contains("project") || text.Contains("meeting") || text.Contains("boss") ||
			    text.Contains("colleague") || text.Contains("client") || text.Contains("email") || text.Contains("task"))
			{
				return "Work";
			}

			// Family-related keywords
			if (text.Contains("family") || text.Contains("mom") || text.Contains("mother") || text.Contains("dad") || 
			    text.Contains("father") || text.Contains("parent") || text.Contains("sibling") || text.Contains("brother") ||
			    text.Contains("sister") || text.Contains("child") || text.Contains("kid") || text.Contains("son") || 
			    text.Contains("daughter") || text.Contains("spouse") || text.Contains("husband") || text.Contains("wife"))
			{
				return "Family";
			}

			// Health-related keywords
			if (text.Contains("health") || text.Contains("doctor") || text.Contains("appointment") || text.Contains("medical") ||
			    text.Contains("exercise") || text.Contains("workout") || text.Contains("gym") || text.Contains("fitness") ||
			    text.Contains("diet") || text.Contains("nutrition") || text.Contains("sleep") || text.Contains("medication"))
			{
				return "Health";
			}

			// Relationships-related keywords
			if (text.Contains("friend") || text.Contains("relationship") || text.Contains("dating") || text.Contains("partner") ||
			    text.Contains("social") || text.Contains("hangout") || text.Contains("party") || text.Contains("gathering"))
			{
				return "Relationships";
			}

			// Personal/Self-care keywords
			if (text.Contains("self") || text.Contains("personal") || text.Contains("hobby") || text.Contains("interest") ||
			    text.Contains("relax") || text.Contains("rest") || text.Contains("me time") || text.Contains("myself"))
			{
				return "Personal";
			}

			return null; // Unknown
		}

		/// <summary>
		/// Determines the emotion tag from brain dump text and tags.
		/// </summary>
		private string? DetermineEmotionTag(string brainDumpText, string? tags)
		{
			var text = $"{brainDumpText} {tags}".ToLower();

			// Emotion keywords mapping
			if (text.Contains("anxious") || text.Contains("anxiety") || text.Contains("worried") || text.Contains("worry"))
				return "Anxious";
			
			if (text.Contains("grateful") || text.Contains("gratitude") || text.Contains("thankful") || text.Contains("appreciate"))
				return "Grateful";
			
			if (text.Contains("overwhelmed") || text.Contains("overwhelm") || text.Contains("too much") || text.Contains("too many"))
				return "Overwhelmed";
			
			if (text.Contains("exhausted") || text.Contains("exhaustion") || text.Contains("tired") || text.Contains("fatigue") || text.Contains("burnout"))
				return "Exhausted";
			
			if (text.Contains("happy") || text.Contains("happiness") || text.Contains("joy") || text.Contains("joyful") || text.Contains("excited"))
				return "Happy";
			
			if (text.Contains("sad") || text.Contains("sadness") || text.Contains("depressed") || text.Contains("depression") || text.Contains("down") || text.Contains("low"))
				return "Sad";
			
			if (text.Contains("calm") || text.Contains("peaceful") || text.Contains("relaxed") || text.Contains("content") || text.Contains("satisfied"))
				return "Calm";
			
			if (text.Contains("frustrated") || text.Contains("frustration") || text.Contains("angry") || text.Contains("anger") || text.Contains("irritated"))
				return "Frustrated";

			return null; // Unknown
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
	private DateTime _lastScheduledDate;
	private TimeSpan _lastScheduledTime;
	private readonly ILogger? _logger;

	public TimeSlotManager((TimeSpan start, TimeSpan end) weekdaySlots, (TimeSpan start, TimeSpan end) weekendSlots, ILogger? logger = null)
	{
		_weekdaySlots = weekdaySlots;
		_weekendSlots = weekendSlots;
		_reservedSlots = new Dictionary<DateTime, List<(TimeSpan start, TimeSpan end)>>();
		_logger = logger;
		
		_logger?.LogInformation("[TIMESLOT_MANAGER] Initializing with weekday slots: {Start}-{End}, weekend slots: {WeekendStart}-{WeekendEnd}", 
			weekdaySlots.start, weekdaySlots.end, weekendSlots.start, weekendSlots.end);
		
		// Initialize to today's current time (if within slots) or start of today's slots
		var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
		var now = DateTime.UtcNow;
		var isWeekend = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;
		var slots = isWeekend ? weekendSlots : weekdaySlots;
		var currentTime = now.TimeOfDay;
		
		_logger?.LogInformation("[TIMESLOT_MANAGER] Today: {Today}, Current UTC time: {CurrentTime}, IsWeekend: {IsWeekend}, Using slots: {Start}-{End}", 
			today.ToString("yyyy-MM-dd"), currentTime, isWeekend, slots.start, slots.end);
		
		// If current time is within today's available slots, start from current time
		// Otherwise, start from the beginning of today's slots (if slots haven't started yet)
		// Or tomorrow if today's slots have already ended
		// Handle midnight-crossing slots (end < start)
		bool isInSlot = false;
		if (slots.end > slots.start)
		{
			// Normal slot: start < end
			isInSlot = currentTime >= slots.start && currentTime < slots.end;
		}
		else
		{
			// Slot crosses midnight: start > end (e.g., 18:00 to 03:00)
			// Time is in slot if it's >= start OR < end
			isInSlot = currentTime >= slots.start || currentTime < slots.end;
		}
		
		if (isInSlot)
		{
			// Current time is within today's slots - start from now
			_lastScheduledDate = today;
			_lastScheduledTime = currentTime;
			_logger?.LogInformation("[TIMESLOT_MANAGER] Current time is within slots, starting from: {Date} {Time}", 
				_lastScheduledDate.ToString("yyyy-MM-dd"), _lastScheduledTime);
		}
		else if (slots.end > slots.start && currentTime < slots.start)
		{
			// Normal slot: current time is before slot start - start from slot start today
			_lastScheduledDate = today;
			_lastScheduledTime = slots.start;
			_logger?.LogInformation("[TIMESLOT_MANAGER] Current time is before slot start, starting from slot start: {Date} {Time}", 
				_lastScheduledDate.ToString("yyyy-MM-dd"), _lastScheduledTime);
		}
		else if (slots.end < slots.start && currentTime < slots.end)
		{
			// Slot crosses midnight: current time is in early morning part (before end) - start from now
			_lastScheduledDate = today;
			_lastScheduledTime = currentTime;
			_logger?.LogInformation("[TIMESLOT_MANAGER] Current time is in early morning part of midnight-crossing slot, starting from: {Date} {Time}", 
				_lastScheduledDate.ToString("yyyy-MM-dd"), _lastScheduledTime);
		}
		else if (slots.end < slots.start && currentTime >= slots.end && currentTime < slots.start)
		{
			// Slot crosses midnight: current time is between end and start (invalid zone) - start from slot start
			_lastScheduledDate = today;
			_lastScheduledTime = slots.start;
			_logger?.LogInformation("[TIMESLOT_MANAGER] Current time is in invalid zone, starting from slot start: {Date} {Time}", 
				_lastScheduledDate.ToString("yyyy-MM-dd"), _lastScheduledTime);
		}
		else
		{
			// Current time is after today's slots end - start from tomorrow
			_lastScheduledDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);
			var tomorrowIsWeekend = _lastScheduledDate.DayOfWeek == DayOfWeek.Saturday || _lastScheduledDate.DayOfWeek == DayOfWeek.Sunday;
			_lastScheduledTime = tomorrowIsWeekend ? weekendSlots.start : weekdaySlots.start;
			_logger?.LogInformation("[TIMESLOT_MANAGER] Current time is after slots end, starting from tomorrow: {Date} {Time}", 
				_lastScheduledDate.ToString("yyyy-MM-dd"), _lastScheduledTime);
		}
	}

    public (DateTime date, TimeSpan time) FindNextAvailableSlot(int durationMinutes)
	{
		var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
		var now = DateTime.UtcNow;
		var currentTime = now.TimeOfDay;
		
		_logger?.LogInformation("[SCHEDULING] FindNextAvailableSlot called - Duration: {Duration}min, Today: {Today}, CurrentTime: {CurrentTime}", 
			durationMinutes, today.ToString("yyyy-MM-dd"), currentTime);
		_logger?.LogInformation("[SCHEDULING] Last scheduled - Date: {LastDate}, Time: {LastTime}", 
			_lastScheduledDate.ToString("yyyy-MM-dd"), _lastScheduledTime);
		
		// Start from the last scheduled time + duration + buffer
		var startDate = _lastScheduledDate;
		var startTime = _lastScheduledTime.Add(TimeSpan.FromMinutes(durationMinutes + _bufferMinutes));
		
		_logger?.LogInformation("[SCHEDULING] Calculated start - Date: {StartDate}, Time: {StartTime}", 
			startDate.ToString("yyyy-MM-dd"), startTime);
		
		// Handle midnight wrap-around: if startTime is after midnight but we're still on the same date,
		// and the slot crosses midnight, we need to check if startTime is still within the slot
		var isWeekendForStartDate = startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday;
		var slotsForStartDate = isWeekendForStartDate ? _weekendSlots : _weekdaySlots;
		
		// If startTime wrapped past midnight (startTime < _lastScheduledTime) and slot crosses midnight,
		// startTime is actually on the next day but still part of the current day's slot
		bool startTimeWrappedPastMidnight = startTime < _lastScheduledTime;
		_logger?.LogInformation("[SCHEDULING] StartDate slots - Start: {Start}, End: {End}, Wrapped: {Wrapped}", 
			slotsForStartDate.start, slotsForStartDate.end, startTimeWrappedPastMidnight);
		
		if (startTimeWrappedPastMidnight && slotsForStartDate.end < slotsForStartDate.start)
		{
			// Time wrapped past midnight, but it's still within the slot (00:00 to 03:00)
			// Keep startDate as is, startTime is correct (e.g., 00:15)
			_logger?.LogInformation("[SCHEDULING] Time wrapped past midnight, slot crosses midnight - keeping startDate: {StartDate}, startTime: {StartTime}", 
				startDate.ToString("yyyy-MM-dd"), startTime);
		}
		else if (startTimeWrappedPastMidnight)
		{
			// Time wrapped past midnight but slot doesn't cross midnight - move to next day
			startDate = DateTime.SpecifyKind(startDate.AddDays(1), DateTimeKind.Utc);
			_logger?.LogInformation("[SCHEDULING] Time wrapped past midnight, slot doesn't cross - moved to next day: {StartDate}", 
				startDate.ToString("yyyy-MM-dd"));
			// startTime is already correct (it's the wrapped time)
		}
		
		var maxDays = 14; // Look ahead 2 weeks

		// First, check if we should start from today (if current time is within today's slots)
		// OR if startDate is tomorrow but startTime is still within today's slot (for midnight-crossing slots)
		var isWeekendForToday = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;
		var slotsForToday = isWeekendForToday ? _weekendSlots : _weekdaySlots;
		bool startTimeStillInTodaySlot = false;
		
		_logger?.LogInformation("[SCHEDULING] Today slots - Start: {Start}, End: {End}, StartDate vs Today: {StartDate} vs {Today}", 
			slotsForToday.start, slotsForToday.end, startDate.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));
		
		if (startDate > today && slotsForToday.end < slotsForToday.start)
		{
			// startDate is tomorrow, but check if startTime is still within today's slot (after midnight portion)
			startTimeStillInTodaySlot = startTime < slotsForToday.end;
			_logger?.LogInformation("[SCHEDULING] Checking if startTime still in today's slot - startTime: {StartTime}, slotEnd: {SlotEnd}, result: {Result}", 
				startTime, slotsForToday.end, startTimeStillInTodaySlot);
		}
		
		if (startDate <= today || startTimeStillInTodaySlot)
		{
			_logger?.LogInformation("[SCHEDULING] Entering TODAY branch - startDate <= today: {Condition1}, startTimeStillInTodaySlot: {Condition2}", 
				startDate <= today, startTimeStillInTodaySlot);
			var isWeekend = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? _weekendSlots : _weekdaySlots;
			
			// Determine the earliest time we can start today
			TimeSpan todayStartTime;
			bool isCurrentTimeInSlot = false;
			if (slots.end > slots.start)
			{
				// Normal slot: start < end (e.g., 12:00 to 21:00)
				isCurrentTimeInSlot = currentTime >= slots.start && currentTime < slots.end;
			}
			else
			{
				// Slot crosses midnight: start > end (e.g., 18:00 to 03:00)
				// Time is in slot if it's >= start OR < end
				isCurrentTimeInSlot = currentTime >= slots.start || currentTime < slots.end;
			}
			
			if (isCurrentTimeInSlot)
			{
				// Current time is within today's slots - start from now
				todayStartTime = currentTime;
			}
			else if (slots.end > slots.start && currentTime < slots.start)
			{
				// Normal slot: current time is before slot start - start from slot start
				todayStartTime = slots.start;
			}
			else if (slots.end < slots.start && currentTime < slots.end)
			{
				// Slot crosses midnight: current time is in early morning part (before end) - start from now
				todayStartTime = currentTime;
			}
			else if (slots.end < slots.start && currentTime >= slots.end && currentTime < slots.start)
			{
				// Slot crosses midnight: current time is between end and start (invalid zone) - start from slot start
				todayStartTime = slots.start;
			}
			else
			{
				// Current time is after today's slots end - skip to tomorrow
				todayStartTime = TimeSpan.Zero; // Will be skipped
			}
			
			// If we can schedule today, try to find a slot starting from todayStartTime
			if (todayStartTime != TimeSpan.Zero)
			{
				// Check if todayStartTime is within the slot window
				bool isWithinSlot = false;
				if (slots.end > slots.start)
				{
					// Normal slot: start < end (e.g., 12:00 to 21:00)
					isWithinSlot = todayStartTime >= slots.start && todayStartTime < slots.end;
				}
				else
				{
					// Slot crosses midnight: start > end (e.g., 18:00 to 03:00)
					// Time is within slot if it's >= start OR < end
					isWithinSlot = todayStartTime >= slots.start || todayStartTime < slots.end;
				}
				
				if (isWithinSlot)
				{
					// Determine the search start time
					TimeSpan searchStartTime;
					if (startDate == today || startTimeStillInTodaySlot)
					{
						// We're continuing from a previous task on the same day, OR
						// startDate is tomorrow but startTime is still within today's slot
						// Check if startTime wrapped past midnight
						bool startTimeWrapped = startTime < _lastScheduledTime;
						
						_logger?.LogInformation("[SCHEDULING] Determining searchStartTime - startDate==today: {Cond1}, startTimeStillInTodaySlot: {Cond2}, startTimeWrapped: {Cond3}", 
							startDate == today, startTimeStillInTodaySlot, startTimeWrapped);
						_logger?.LogInformation("[SCHEDULING] Values - startTime: {StartTime}, todayStartTime: {TodayStartTime}, lastScheduledTime: {LastTime}", 
							startTime, todayStartTime, _lastScheduledTime);
						
						if (startTimeWrapped && slots.end < slots.start)
						{
							// startTime wrapped past midnight (e.g., 23:30 + 45 min = 00:15)
							// and slot crosses midnight - use startTime (e.g., 00:15)
							// This time is still part of today's slot (extends to 03:00 UTC next day)
							searchStartTime = startTime;
							_logger?.LogInformation("[SCHEDULING] Using startTime (wrapped, slot crosses midnight): {SearchStartTime}", searchStartTime);
						}
						else if (startTimeStillInTodaySlot)
						{
							// startDate is tomorrow but startTime is still in today's slot (e.g., 01:00 UTC on Jan 1st, but still in Dec 31st's slot)
							searchStartTime = startTime;
							_logger?.LogInformation("[SCHEDULING] Using startTime (still in today's slot): {SearchStartTime}", searchStartTime);
						}
						else if (!startTimeWrapped && startTime > todayStartTime)
						{
							// startTime didn't wrap and is later than todayStartTime, use it
							searchStartTime = startTime;
							_logger?.LogInformation("[SCHEDULING] Using startTime (didn't wrap, later than todayStartTime): {SearchStartTime}", searchStartTime);
						}
						else
						{
							// Use todayStartTime (either it's later, or startTime wrapped but slot doesn't cross midnight)
							searchStartTime = todayStartTime;
							_logger?.LogInformation("[SCHEDULING] Using todayStartTime: {SearchStartTime}", searchStartTime);
						}
					}
					else
					{
						// We're starting fresh today
						searchStartTime = todayStartTime;
						_logger?.LogInformation("[SCHEDULING] Starting fresh today, using todayStartTime: {SearchStartTime}", searchStartTime);
					}
					
					// Ensure searchStartTime is at least at the slot start (for normal slots)
					// For midnight-crossing slots, we handle this in FindAvailableTimeInDayFromTime
					if (slots.end > slots.start && searchStartTime < slots.start)
					{
						searchStartTime = slots.start;
					}
					
					// Check if searchStartTime is still within slot (handle midnight crossing)
					bool searchTimeInSlot = false;
					if (slots.end > slots.start)
					{
						// Normal slot
						searchTimeInSlot = searchStartTime >= slots.start && searchStartTime < slots.end;
					}
					else
					{
						// Slot crosses midnight: time is in slot if it's >= start OR < end
						searchTimeInSlot = searchStartTime >= slots.start || searchStartTime < slots.end;
					}
					
					if (searchTimeInSlot)
					{
						_logger.LogInformation("[SCHEDULING] searchTimeInSlot is true, calling FindAvailableTimeInDayFromTime - date: {Date}, slots: {Start}-{End}, searchStartTime: {SearchStart}, duration: {Duration}", 
							today.ToString("yyyy-MM-dd"), slots.start, slots.end, searchStartTime, durationMinutes);
						var availableTime = FindAvailableTimeInDayFromTime(today, slots, searchStartTime, durationMinutes);
						_logger.LogInformation("[SCHEDULING] FindAvailableTimeInDayFromTime returned: {AvailableTime}", availableTime);
						
						if (availableTime != TimeSpan.Zero)
						{
							// For midnight-crossing slots, if the available time is after midnight (before slot end),
							// it should be on the next day
							DateTime resultDate = today;
							if (slots.end < slots.start && availableTime < slots.end)
							{
								// Time is after midnight, use next day
								resultDate = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc);
								_logger.LogInformation("[SCHEDULING] Available time is after midnight, using next day: {ResultDate}", resultDate.ToString("yyyy-MM-dd"));
							}
							_logger.LogInformation("[SCHEDULING] Returning slot - Date: {Date}, Time: {Time}", resultDate.ToString("yyyy-MM-dd"), availableTime);
							return (resultDate, availableTime);
						}
						else
						{
							_logger.LogWarning("[SCHEDULING] No available time found in today's slot!");
						}
					}
					else if (slots.end > slots.start && searchStartTime < slots.start)
					{
						// For normal slots, if searchStartTime is before slot start, try from slot start
						var availableTime = FindAvailableTimeInDayFromTime(today, slots, slots.start, durationMinutes);
						if (availableTime != TimeSpan.Zero)
						{
							return (today, availableTime);
						}
					}
                    else
                    {
                        _logger.LogWarning("[SCHEDULING] searchTimeInSlot is false - searchStartTime: {SearchStart}, slots: {Start}-{End}",
                            searchStartTime, slots.start, slots.end);
                    }
                }
			}
			
			// No time available today, move to tomorrow
			_logger?.LogInformation("[SCHEDULING] No time available today, moving to tomorrow");
			startDate = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc);
			var tomorrowIsWeekend = startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday;
			startTime = tomorrowIsWeekend ? _weekendSlots.start : _weekdaySlots.start;
			_logger?.LogInformation("[SCHEDULING] Tomorrow - Date: {Date}, Time: {Time}", startDate.ToString("yyyy-MM-dd"), startTime);
		}
		else
		{
			// We're already scheduling in the future, check if there's time on the same day
			_logger?.LogInformation("[SCHEDULING] Entering ELSE branch - startDate is in the future: {StartDate}", startDate.ToString("yyyy-MM-dd"));
			var isWeekend = startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? _weekendSlots : _weekdaySlots;
			
			// Check if startTime is still within the same day's slot window (handle midnight crossing)
			bool isStartTimeInSlot = false;
			if (slots.end > slots.start)
			{
				// Normal slot: start < end
				isStartTimeInSlot = startTime >= slots.start && startTime < slots.end;
			}
			else
			{
				// Slot crosses midnight: start > end
				// Time is in slot if it's >= start OR < end
				isStartTimeInSlot = startTime >= slots.start || startTime < slots.end;
			}
			
			if (isStartTimeInSlot)
			{
				var availableTime = FindAvailableTimeInDayFromTime(startDate, slots, startTime, durationMinutes);
				if (availableTime != TimeSpan.Zero)
				{
					// For midnight-crossing slots, if the available time is after midnight (before slot end),
					// it should be on the next day
					DateTime resultDate = startDate;
					if (slots.end < slots.start && availableTime < slots.end)
					{
						// Time is after midnight, use next day
						resultDate = DateTime.SpecifyKind(startDate.AddDays(1), DateTimeKind.Utc);
					}
					return (resultDate, availableTime);
				}
			}
			
			// No time available on same day, move to next day
			startDate = startDate.AddDays(1);
			var nextDayIsWeekend = startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday;
			startTime = nextDayIsWeekend ? _weekendSlots.start : _weekdaySlots.start;
		}

		// Search from the calculated start date
		var dayOffset = (int)(startDate.Date - today.AddDays(1)).TotalDays;
		for (int i = dayOffset; i < maxDays; i++)
		{
			var date = DateTime.SpecifyKind(today.AddDays(1 + i), DateTimeKind.Utc);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var slots = isWeekend ? _weekendSlots : _weekdaySlots;

			// Find available time within the day's slots
			var availableTime = FindAvailableTimeInDay(date, slots, durationMinutes);
			if (availableTime != TimeSpan.Zero)
			{
				// For midnight-crossing slots, if the available time is after midnight (before slot end),
				// it should be on the next day
				DateTime resultDate = date;
				if (slots.end < slots.start && availableTime < slots.end)
				{
					// Time is after midnight, use next day
					resultDate = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Utc);
				}
				return (resultDate, availableTime);
			}
		}

        // Fallback: return tomorrow at start of available slots (UTC)
        var fallbackDate = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc);
		var fallbackIsWeekend = fallbackDate.DayOfWeek == DayOfWeek.Saturday || fallbackDate.DayOfWeek == DayOfWeek.Sunday;
		var fallbackSlots = fallbackIsWeekend ? _weekendSlots : _weekdaySlots;
		return (fallbackDate, fallbackSlots.start);
	}

    public (DateTime date, TimeSpan time) FindSlotMatchingTime(TimeSpan preferredTime, int durationMinutes)
	{
        // Use UTC date explicitly
        var startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);
		var maxDays = 14;

		for (int dayOffset = 0; dayOffset < maxDays; dayOffset++)
		{
			var date = DateTime.SpecifyKind(startDate.AddDays(dayOffset).Date, DateTimeKind.Utc);
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

		return (DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), TimeSpan.Zero);
	}

    public void ReserveSlot(DateTime date, TimeSpan time, int durationMinutes)
	{
        // Ensure date is UTC and normalize to date-only key
        var dateKey = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        if (!_reservedSlots.ContainsKey(dateKey))
		{
            _reservedSlots[dateKey] = new List<(TimeSpan start, TimeSpan end)>();
		}

		var endTime = time.Add(TimeSpan.FromMinutes(durationMinutes + _bufferMinutes));
        _reservedSlots[dateKey].Add((time, endTime));
		
		_logger?.LogInformation("[RESERVE] Reserving slot - Date: {Date}, Time: {Time}, Duration: {Duration}min, EndTime: {EndTime}", 
			dateKey.ToString("yyyy-MM-dd"), time, durationMinutes, endTime);
		
		// Log all currently reserved slots for this date
		if (_reservedSlots.ContainsKey(dateKey))
		{
			_logger?.LogInformation("[RESERVE] Current reserved slots for {Date}: {Count} slots", 
				dateKey.ToString("yyyy-MM-dd"), _reservedSlots[dateKey].Count);
			foreach (var slot in _reservedSlots[dateKey])
			{
				_logger?.LogInformation("[RESERVE]   - {Start} to {End}", slot.start, slot.end);
			}
		}
		
		// Update last scheduled date and time to track where we are
		if (dateKey > _lastScheduledDate || (dateKey == _lastScheduledDate && time > _lastScheduledTime))
		{
			var oldDate = _lastScheduledDate;
			var oldTime = _lastScheduledTime;
			_lastScheduledDate = dateKey;
			_lastScheduledTime = time;
			_logger?.LogInformation("[RESERVE] Updated last scheduled - Old: {OldDate} {OldTime}, New: {NewDate} {NewTime}", 
				oldDate.ToString("yyyy-MM-dd"), oldTime, dateKey.ToString("yyyy-MM-dd"), time);
		}
	}

	private TimeSpan FindAvailableTimeInDay(DateTime date, (TimeSpan start, TimeSpan end) slots, int durationMinutes)
	{
		return FindAvailableTimeInDayFromTime(date, slots, slots.start, durationMinutes);
	}

	private TimeSpan FindAvailableTimeInDayFromTime(DateTime date, (TimeSpan start, TimeSpan end) slots, TimeSpan startFromTime, int durationMinutes)
	{
		// Handle windows that may cross midnight (e.g., 18:00 -> 03:00)
		var start = slots.start;
		var end = slots.end;
		
		// Determine the actual scan start time based on slot type
		TimeSpan scanStart;
		if (end > start)
		{
			// Normal slot: start < end (e.g., 12:00 to 21:00)
			// Use the later of slot start or the requested start time, but ensure it's within the slot
			if (startFromTime >= start && startFromTime < end)
			{
				scanStart = startFromTime;
			}
			else if (startFromTime < start)
			{
				// Requested time is before slot start, use slot start
				scanStart = start;
			}
			else
			{
				// Requested time is after slot end, no time available
				return TimeSpan.Zero;
			}
		}
		else
		{
			// Slot crosses midnight: start > end (e.g., 18:00 to 03:00)
			// Time is in slot if it's >= start OR < end
			if (startFromTime >= start || startFromTime < end)
			{
				// Requested time is within the slot
				scanStart = startFromTime;
			}
			else if (startFromTime >= end && startFromTime < start)
			{
				// Requested time is between end and start (invalid zone), use slot start
				scanStart = start;
			}
			else
			{
				// This shouldn't happen, but handle it
				scanStart = start;
			}
		}

		TimeSpan Scan(TimeSpan segStart, TimeSpan segEnd, bool isAfterMidnight = false)
		{
			_logger?.LogInformation("[FIND_SLOT] Scan called - segStart: {SegStart}, segEnd: {SegEnd}, isAfterMidnight: {AfterMidnight}, baseDate: {BaseDate}", 
				segStart, segEnd, isAfterMidnight, date.ToString("yyyy-MM-dd"));
			
			var t = segStart;
			// Ensure we don't exceed the segment end
			var maxTime = segEnd;
			if (segEnd < segStart)
			{
				// Segment crosses midnight, use 24:00 as max
				maxTime = new TimeSpan(24, 0, 0);
			}
			
			// For after-midnight times, we need to check the next day
			var checkDate = isAfterMidnight ? date.AddDays(1) : date;
			_logger?.LogInformation("[FIND_SLOT] Scanning from {T} to {MaxTime}, checking date: {CheckDate} (isAfterMidnight: {AfterMidnight})", 
				t, maxTime, checkDate.ToString("yyyy-MM-dd"), isAfterMidnight);
			
			int attempts = 0;
			int checkedCount = 0;
			while (t.Add(TimeSpan.FromMinutes(durationMinutes)) <= maxTime && attempts < 100)
			{
				checkedCount++;
				var isAvailable = IsTimeSlotAvailable(checkDate, t, durationMinutes);
				_logger?.LogInformation("[FIND_SLOT] Attempt {Attempt}: Checking time {Time} on {Date} - Available: {Available}", 
					checkedCount, t, checkDate.ToString("yyyy-MM-dd"), isAvailable);
				
				if (isAvailable)
				{
					_logger?.LogInformation("[FIND_SLOT] Found available time: {Time} on {Date}", t, checkDate.ToString("yyyy-MM-dd"));
					return t;
				}
				t = t.Add(TimeSpan.FromMinutes(30));
				attempts++;
				
				// Prevent infinite loop if we somehow exceed 24 hours
				if (t.TotalMinutes >= 24 * 60)
				{
					_logger?.LogWarning("[FIND_SLOT] Time exceeded 24 hours, breaking loop");
					break;
				}
			}
			_logger?.LogWarning("[FIND_SLOT] No available time found in segment {SegStart}-{SegEnd} after {CheckedCount} attempts", 
				segStart, segEnd, checkedCount);
			return TimeSpan.Zero;
		}

		if (end > start)
		{
			// Normal case: start < end (e.g., 12:00 to 21:00)
			return Scan(scanStart, end);
		}

		// Handle case where end < start (crosses midnight, e.g., 18:00 to 03:00)
		if (scanStart >= start)
		{
			// Scan from scanStart to end of day (midnight) - these times are on the current date
			_logger?.LogInformation("[FIND_SLOT] scanStart >= start, scanning first segment from {ScanStart} to 24:00 on {Date}", 
				scanStart, date.ToString("yyyy-MM-dd"));
			var first = Scan(scanStart, new TimeSpan(24, 0, 0), isAfterMidnight: false);
			if (first != TimeSpan.Zero)
			{
				_logger?.LogInformation("[FIND_SLOT] Found time in first segment: {Time}", first);
				return first;
			}
			// Then scan from start of day (00:00) to end (03:00) - these times are on the next day
			_logger?.LogInformation("[FIND_SLOT] No time found in first segment, scanning second segment from 00:00 to {End} on next day", end);
			return Scan(TimeSpan.Zero, end, isAfterMidnight: true);
		}
		else if (scanStart < end)
		{
			// scanStart is in the early morning part (before end, e.g., 01:00) - these times are on the next day
			_logger?.LogInformation("[FIND_SLOT] scanStart < end, scanning from {ScanStart} to {End} on next day", scanStart, end);
			return Scan(scanStart, end, isAfterMidnight: true);
		}
		else
		{
			// scanStart is between end and start (invalid zone), scan from start
			_logger?.LogInformation("[FIND_SLOT] scanStart is in invalid zone, scanning from start {Start} to 24:00, then 00:00 to {End}", start, end);
			var first = Scan(start, new TimeSpan(24, 0, 0), isAfterMidnight: false);
			if (first != TimeSpan.Zero) return first;
			return Scan(TimeSpan.Zero, end, isAfterMidnight: true);
		}
	}

    private bool IsTimeSlotAvailable(DateTime date, TimeSpan time, int durationMinutes)
	{
        var dateKey = date.Date; // normalize to date-only key (UTC date)
        if (!_reservedSlots.ContainsKey(dateKey))
		{
			_logger?.LogDebug("[IS_AVAILABLE] No reserved slots for date {Date}, returning true", dateKey.ToString("yyyy-MM-dd"));
			return true;
		}

		var requestedStart = time;
		var requestedEnd = time.Add(TimeSpan.FromMinutes(durationMinutes + _bufferMinutes));
		
		_logger?.LogDebug("[IS_AVAILABLE] Checking date {Date}, time {Time}, duration {Duration}min - requested range: {Start} to {End}", 
			dateKey.ToString("yyyy-MM-dd"), time, durationMinutes, requestedStart, requestedEnd);
		_logger?.LogDebug("[IS_AVAILABLE] Reserved slots for this date: {Count}", _reservedSlots[dateKey].Count);

        foreach (var reservedSlot in _reservedSlots[dateKey])
		{
			_logger?.LogDebug("[IS_AVAILABLE] Checking against reserved slot: {Start} to {End}", reservedSlot.start, reservedSlot.end);
			// Check for overlap
			if (requestedStart < reservedSlot.end && requestedEnd > reservedSlot.start)
			{
				_logger?.LogWarning("[IS_AVAILABLE] CONFLICT FOUND - Requested {Start}-{End} overlaps with reserved {ReservedStart}-{ReservedEnd}", 
					requestedStart, requestedEnd, reservedSlot.start, reservedSlot.end);
				return false;
			}
		}

		_logger?.LogDebug("[IS_AVAILABLE] No conflicts found, slot is available");
		return true;
	}
}



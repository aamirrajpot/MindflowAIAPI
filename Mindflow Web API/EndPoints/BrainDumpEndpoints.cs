using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Utilities;
using Mindflow_Web_API.Exceptions;
using System.Security.Claims;

namespace Mindflow_Web_API.EndPoints
{
	public static class BrainDumpEndpoints
	{
		public static IEndpointRouteBuilder MapBrainDumpEndpoints(this IEndpointRouteBuilder app)
		{
			var api = app.MapGroup("/brain-dump").WithTags("Brain Dump").RequireAuthorization();

			api.MapPost("/suggestions", async (
				[FromBody] BrainDumpRequest request,
				IBrainDumpService service,
				ISubscriptionService subscriptionService,
				HttpContext ctx,
				MindflowDbContext db,
				[FromQuery] int maxTokens = 1200,
				[FromQuery] double temperature = 0.7) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				// Check if user has an active subscription (not free user and not expired)
				var hasActiveSubscription = await subscriptionService.IsSubscriptionActiveAsync(userId);
				if (!hasActiveSubscription)
				{
					throw ApiExceptions.Forbidden("An active subscription is required to use brain dump features. Please subscribe to continue.");
				}

				// Generate comprehensive brain dump analysis
				var brainDumpResponse = await service.GetTaskSuggestionsAsync(userId, request, maxTokens, temperature);

				return Results.Ok(brainDumpResponse);
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Get comprehensive brain dump analysis";
				op.Description = "Accepts free-form text and returns user profile summary, key themes, AI summary, and personalized task suggestions";
				return op;
			});

			api.MapPost("/add-to-calendar", async (
				[FromBody] AddToCalendarRequest request,
				IBrainDumpService service,
				HttpContext ctx) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				try
				{
					var taskItem = await service.AddTaskToCalendarAsync(userId, request);

					return Results.Ok(new
					{
						message = "Task added to calendar successfully",
						taskId = taskItem.Id,
						task = new
						{
							taskItem.Title,
							taskItem.Description,
							taskItem.Category,
							taskItem.Date,
							taskItem.Time,
							taskItem.DurationMinutes,
							taskItem.RepeatType,
							// Brain dump linking fields (Actionable Value feature)
							taskItem.SourceBrainDumpEntryId,
							taskItem.SourceTextExcerpt,
							taskItem.LifeArea,
							taskItem.EmotionTag
						}
					});
				}
				catch (ArgumentException ex)
				{
					return Results.BadRequest(ex.Message);
				}
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Add a suggested task to calendar";
				op.Description = "Creates a TaskItem in the database from a task suggestion";
				return op;
			});

			api.MapPost("/add-multiple-to-calendar", async (
				[FromBody] AddMultipleTasksRequest request,
				IBrainDumpService service,
				HttpContext ctx) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

			try
			{
				// Get database context to query TaskSuggestionRecords
				var dbContext = ctx.RequestServices.GetRequiredService<MindflowDbContext>();
				
				// Extract suggestion IDs from the request (assuming only IDs are provided)
				var suggestionIds = request.Suggestions
					.Where(s => s.Id.HasValue)
					.Select(s => s.Id!.Value)
					.Distinct()
					.ToList();
				
				if (suggestionIds.Count == 0)
				{
					return Results.BadRequest(new { message = "No valid suggestion IDs provided" });
				}

				// Fetch task suggestion records from database based on IDs
				var suggestionRecords = await dbContext.TaskSuggestionRecords
					.Where(r => suggestionIds.Contains(r.Id) && r.UserId == userId)
					.ToListAsync();

				if (suggestionRecords.Count == 0)
				{
					return Results.NotFound(new { message = "No task suggestions found for the provided IDs" });
				}

				// Check if all IDs were found
				if (suggestionRecords.Count != suggestionIds.Count)
				{
					var foundIds = suggestionRecords.Select(r => r.Id).ToList();
					var missingIds = suggestionIds.Except(foundIds).ToList();
					// Log warning but continue with found records
					var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BrainDumpEndpoints");
					logger.LogWarning("Some suggestion IDs were not found: {MissingIds}", string.Join(", ", missingIds));
				}

				var taskItems = new List<TaskItem>();
				var errors = new List<string>();

				// Process tasks sequentially to avoid race conditions in conflict detection
				// Each task needs to see previously scheduled tasks to avoid conflicts
				foreach (var suggestionRecord in suggestionRecords)
				{
					try
					{
						// Convert TaskSuggestionRecord to AddToCalendarRequest
						var addRequest = new AddToCalendarRequest
						{
							Task = suggestionRecord.Task,
							Frequency = suggestionRecord.Frequency ?? "once",
							Duration = suggestionRecord.Duration ?? "30 minutes",
							Notes = suggestionRecord.Notes,
							BrainDumpEntryId = request.BrainDumpEntryId ?? suggestionRecord.BrainDumpEntryId,
							Urgency = suggestionRecord.Urgency,
							Importance = suggestionRecord.Importance,
							PriorityScore = suggestionRecord.PriorityScore,
							ReminderEnabled = false // Default to false, can be made configurable
						};

						var taskItem = await service.AddTaskToCalendarAsync(userId, addRequest);
						
						// Update the suggestion record to mark it as scheduled
						suggestionRecord.Status = TaskSuggestionStatus.Scheduled;
						suggestionRecord.TaskItemId = taskItem.Id;
						
						taskItems.Add(taskItem);
						
						// Save suggestion record status immediately so subsequent tasks can see this one when checking for conflicts
						// Note: The task itself is already saved by AddTaskToCalendarAsync
						await dbContext.SaveChangesAsync();
					}
					catch (Exception ex)
					{
						errors.Add($"Failed to add task '{suggestionRecord.Task}': {ex.Message}");
					}
				}

				if (errors.Count > 0 && taskItems.Count == 0)
				{
					return Results.BadRequest(new
					{
						message = "Failed to add any tasks to calendar",
						errors = errors
					});
				}

				return Results.Ok(new
				{
					message = $"Successfully added {taskItems.Count} of {request.Suggestions.Count} tasks to calendar",
					taskCount = taskItems.Count,
					errors = errors.Count > 0 ? errors : null,
					tasks = taskItems.Select(t => new
					{
						Id = t.Id,
						Title = t.Title,
						Description = t.Description,
						Category = t.Category,
						Date = t.Date,
						Time = t.Time,
						DurationMinutes = t.DurationMinutes,
						RepeatType = t.RepeatType,
						SourceBrainDumpEntryId = t.SourceBrainDumpEntryId,
						SourceTextExcerpt = t.SourceTextExcerpt,
						LifeArea = t.LifeArea,
						EmotionTag = t.EmotionTag
					}).ToList()
				});
			}
				catch (ArgumentException ex)
				{
					return Results.BadRequest(ex.Message);
				}
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Add multiple suggested tasks to calendar with smart scheduling";
				op.Description = "Creates multiple TaskItems in the database with optimal scheduling based on available time slots";
				return op;
			});

			api.MapGet("/test-scheduling", async (
				IBrainDumpService service,
				HttpContext ctx) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				// Create sample task suggestions for testing
				var sampleSuggestions = new List<TaskSuggestion>
				{
					new TaskSuggestion
					{
						Task = "Call doctor for appointment",
						Frequency = "Once",
						Duration = "15 minutes",
						Notes = "Schedule annual checkup",
						Priority = "High",
						SuggestedTime = "Morning"
					},
					new TaskSuggestion
					{
						Task = "Organize home office",
						Frequency = "Once",
						Duration = "60 minutes",
						Notes = "Clean desk and organize files",
						Priority = "Medium",
						SuggestedTime = "Afternoon"
					},
					new TaskSuggestion
					{
						Task = "Buy groceries",
						Frequency = "Weekly",
						Duration = "45 minutes",
						Notes = "Weekly shopping trip",
						Priority = "Medium",
						SuggestedTime = "Evening"
					},
					new TaskSuggestion
					{
						Task = "Read for 30 minutes",
						Frequency = "Daily",
						Duration = "30 minutes",
						Notes = "Personal development reading",
						Priority = "Low",
						SuggestedTime = "Evening"
					}
				};

				// NOTE: The old multi-task scheduling method has been removed to simplify the scheduling model.
				// This endpoint remains as a placeholder/example and currently does not perform any operations.
				return Results.BadRequest(new { error = "Bulk task scheduling is currently disabled." });
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Test the new smart scheduling functionality";
				op.Description = "Demonstrates how both single and multiple tasks are now scheduled across available time slots from wellness check-ins";
				return op;
			});

			api.MapPost("/auto-schedule-all", async (
				[FromBody] AutoScheduleAllRequest request,
				IBrainDumpService service,
				HttpContext ctx) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				try
				{
					var taskItems = await service.AutoScheduleAllTasksAsync(userId, request.BrainDumpEntryId, request.SuggestionIds);

					return Results.Ok(new
					{
						message = $"Successfully auto-scheduled {taskItems.Count} tasks",
						taskCount = taskItems.Count,
						tasks = taskItems.Select(t => new
						{
							Id = t.Id,
							Title = t.Title,
							Description = t.Description,
							Category = t.Category,
							Date = t.Date,
							Time = t.Time,
							DurationMinutes = t.DurationMinutes,
							RepeatType = t.RepeatType,
							SourceBrainDumpEntryId = t.SourceBrainDumpEntryId,
							SourceTextExcerpt = t.SourceTextExcerpt,
							LifeArea = t.LifeArea,
							EmotionTag = t.EmotionTag,
							Urgency = t.Urgency,
							Importance = t.Importance,
							PriorityScore = t.PriorityScore,
							SubSteps = t.SubSteps != null ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(t.SubSteps) : null
						}).ToList()
					});
				}
				catch (ArgumentException ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Auto-schedule all suggested tasks from a brain dump";
				op.Description = "Automatically schedules all suggested tasks from a brain dump entry using smart scheduling across available time slots. Tasks are saved to the database and scheduled optimally based on user's wellness check-in data.";
				return op;
			});

			api.MapPost("/skip-tasks", async (
				[FromBody] SkipTasksRequest request,
				IBrainDumpService service,
				HttpContext ctx) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				try
				{
					var success = await service.SkipTasksAsync(userId, request.BrainDumpEntryId, request.SuggestionIds);

					return Results.Ok(new
					{
						message = "Tasks skipped successfully",
						success = success
					});
				}
				catch (ArgumentException ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Skip suggested tasks from a brain dump";
				op.Description = "Marks task suggestions as skipped. If no suggestionIds are provided, all remaining suggested tasks for the brain dump entry are marked skipped.";
				return op;
			});

			// Get analytics endpoint
			api.MapGet("/analytics", async (
				IBrainDumpService service,
				HttpContext ctx,
				[FromQuery] Guid? brainDumpEntryId = null) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				try
				{
					var analytics = await service.GetAnalyticsAsync(userId, brainDumpEntryId);
					return Results.Ok(analytics);
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Get brain dump analytics";
				op.Description = "Returns analytics data including insights, patterns, progress metrics, emotion trends, and personalized message. Optionally accepts a brainDumpEntryId to include context from a specific entry.";
				return op;
			});

			return app;
		}
	}
}



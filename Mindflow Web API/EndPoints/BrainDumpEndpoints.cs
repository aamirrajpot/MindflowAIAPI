using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Utilities;

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
				HttpContext ctx,
				MindflowDbContext db,
				[FromQuery] int maxTokens = 1200,
				[FromQuery] double temperature = 0.7) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

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
					var taskItems = await service.AddMultipleTasksToCalendarAsync(userId, request.Suggestions, null, request.BrainDumpEntryId);

					return Results.Ok(new
					{
						message = $"Successfully added {taskItems.Count} tasks to calendar",
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

				try
				{
					var taskItems = await service.AddMultipleTasksToCalendarAsync(userId, sampleSuggestions);

					return Results.Ok(new
					{
						message = "Test scheduling completed successfully",
						description = "This demonstrates the new smart scheduling functionality for both single and multiple tasks",
						improvements = new[]
						{
							"✅ Single task API now uses smart scheduling",
							"✅ Multiple tasks API distributes across available time slots",
							"✅ Respects weekday vs weekend availability from wellness check-in",
							"✅ No overlapping schedules with 15-minute buffers",
							"✅ AI suggestions are considered but not enforced",
							"✅ Priority-based scheduling (High → Medium → Low)",
							"✅ User preferences (date/time) are respected when possible"
						},
						taskCount = taskItems.Count,
						scheduledTasks = taskItems.Select(t => new
						{
							Title = t.Title,
							Description = t.Description,
							Category = t.Category,
							scheduledDate = t.Date.ToString("yyyy-MM-dd"),
							scheduledTime = t.Time.ToString("HH:mm"),
							DurationMinutes = t.DurationMinutes,
							RepeatType = t.RepeatType
						}).ToList()
					});
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Test the new smart scheduling functionality";
				op.Description = "Demonstrates how both single and multiple tasks are now scheduled across available time slots from wellness check-ins";
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



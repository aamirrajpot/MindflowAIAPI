using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;

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
							taskItem.RepeatType
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

			return app;
		}
	}
}



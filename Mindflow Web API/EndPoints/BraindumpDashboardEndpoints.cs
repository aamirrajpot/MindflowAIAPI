using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Mindflow_Web_API.EndPoints
{
	public static class BraindumpDashboardEndpoints
	{
		public static void MapBraindumpDashboardEndpoints(this IEndpointRouteBuilder app)
		{
			var dashboard = app.MapGroup("/braindump-dashboard").WithTags("BraindumpDashboard");

			// Get today's tasks with a minimal summary for the dashboard
			dashboard.MapGet("/tasks/today", async (ITaskItemService taskService, HttpContext context) =>
			{
				if (!context.User.Identity?.IsAuthenticated ?? true)
					throw ApiExceptions.Unauthorized("User is not authenticated");
				var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					throw ApiExceptions.Unauthorized("Invalid user token");

				var today = DateTime.UtcNow.Date;
				var tasks = (await taskService.GetTasksWithRecurringAsync(userId, today)).ToList();
				var total = tasks.Count;
				var completed = tasks.Count(t => t.Status == Models.TaskStatus.Completed);
				var progress = total == 0 ? 0 : (int)Math.Round((double)completed * 100 / total);

				return Results.Ok(new
				{
					date = today,
					total,
					completed,
					progressPercent = progress,
					tasks
				});
			})
			.RequireAuthorization()
			.WithOpenApi(op =>
			{
				op.Summary = "Get today's tasks for the braindump dashboard";
				op.Description = "Returns today's tasks for the authenticated user along with a simple completion summary.";
				return op;
			});

			// Toggle a task between Pending and Completed for quick dashboard interaction
			dashboard.MapPatch("/tasks/{taskId:guid}/toggle", async (Guid taskId, ITaskItemService taskService, HttpContext context) =>
			{
				if (!context.User.Identity?.IsAuthenticated ?? true)
					throw ApiExceptions.Unauthorized("User is not authenticated");
				var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					throw ApiExceptions.Unauthorized("Invalid user token");

				var existing = await taskService.GetByIdAsync(userId, taskId);
				if (existing == null)
					throw ApiExceptions.NotFound("Task not found");

				var newStatus = existing.Status == Models.TaskStatus.Completed
					? Models.TaskStatus.Pending
					: Models.TaskStatus.Completed;

				var updated = await taskService.UpdateStatusAsync(userId, taskId, newStatus);
				return Results.Ok(updated);
			})
			.RequireAuthorization()
			.WithOpenApi(op =>
			{
				op.Summary = "Toggle task completion for braindump dashboard";
				op.Description = "Toggles a task's status between Pending and Completed for the authenticated user.";
				return op;
			});

			// Wellness Snapshot endpoints for dashboard
			var wellness = app.MapGroup("/wellness").WithTags("Wellness Snapshot");

			// Get wellness snapshot for the last N days
			wellness.MapGet("/snapshot", async (IWellnessSnapshotService wellnessService, HttpContext context, [FromQuery] int days = 7) =>
			{
				if (!context.User.Identity?.IsAuthenticated ?? true)
					throw ApiExceptions.Unauthorized("User is not authenticated");
				
				var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					throw ApiExceptions.Unauthorized("Invalid user token");

				if (days < 1 || days > 365)
					throw ApiExceptions.BadRequest("Days parameter must be between 1 and 365");

				var snapshot = await wellnessService.GetWellnessSnapshotAsync(userId, days);
				return Results.Ok(snapshot);
			})
			.RequireAuthorization()
			.WithOpenApi(op =>
			{
				op.Summary = "Get wellness snapshot";
				op.Description = "Returns a wellness snapshot with mood, energy, and stress data over the specified number of days (default 7 days).";
				op.Parameters[0].Description = "Number of days to include in the snapshot (1-365)";
				return op;
			});

			// Get wellness snapshot for a specific date range
			wellness.MapGet("/snapshot/range", async (IWellnessSnapshotService wellnessService, HttpContext context, 
				[FromQuery] DateTime startDate, [FromQuery] DateTime endDate) =>
			{
				if (!context.User.Identity?.IsAuthenticated ?? true)
					throw ApiExceptions.Unauthorized("User is not authenticated");
				
				var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					throw ApiExceptions.Unauthorized("Invalid user token");

				if (startDate > endDate)
					throw ApiExceptions.BadRequest("Start date must be before end date");

				if ((endDate - startDate).Days > 365)
					throw ApiExceptions.BadRequest("Date range cannot exceed 365 days");

				var snapshot = await wellnessService.GetWellnessSnapshotForPeriodAsync(userId, startDate.Date, endDate.Date);
				return Results.Ok(snapshot);
			})
			.RequireAuthorization()
			.WithOpenApi(op =>
			{
				op.Summary = "Get wellness snapshot for date range";
				op.Description = "Returns a wellness snapshot with mood, energy, and stress data for the specified date range.";
				op.Parameters[0].Description = "Start date (YYYY-MM-DD format)";
				op.Parameters[1].Description = "End date (YYYY-MM-DD format)";
				return op;
			});

			// Get weekly wellness snapshot (last 7 days)
			wellness.MapGet("/snapshot/weekly", async (IWellnessSnapshotService wellnessService, HttpContext context) =>
			{
				if (!context.User.Identity?.IsAuthenticated ?? true)
					throw ApiExceptions.Unauthorized("User is not authenticated");
				
				var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					throw ApiExceptions.Unauthorized("Invalid user token");

				var snapshot = await wellnessService.GetWellnessSnapshotAsync(userId, 7);
				return Results.Ok(snapshot);
			})
			.RequireAuthorization()
			.WithOpenApi(op =>
			{
				op.Summary = "Get weekly wellness snapshot";
				op.Description = "Returns a wellness snapshot for the last 7 days, perfect for weekly charts.";
				return op;
			});

			// Get monthly wellness snapshot (last 30 days)
			wellness.MapGet("/snapshot/monthly", async (IWellnessSnapshotService wellnessService, HttpContext context) =>
			{
				if (!context.User.Identity?.IsAuthenticated ?? true)
					throw ApiExceptions.Unauthorized("User is not authenticated");
				
				var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					throw ApiExceptions.Unauthorized("Invalid user token");

				var snapshot = await wellnessService.GetWellnessSnapshotAsync(userId, 30);
				return Results.Ok(snapshot);
			})
			.RequireAuthorization()
			.WithOpenApi(op =>
			{
				op.Summary = "Get monthly wellness snapshot";
				op.Description = "Returns a wellness snapshot for the last 30 days, perfect for monthly trend analysis.";
				return op;
			});
		}
	}
}



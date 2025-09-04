using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;

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
		}
	}
}



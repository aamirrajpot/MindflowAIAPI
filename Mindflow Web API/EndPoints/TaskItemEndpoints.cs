using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.EndPoints
{
    public static class TaskItemEndpoints
    {
        public static void MapTaskItemEndpoints(this IEndpointRouteBuilder app)
        {
            var tasksApi = app.MapGroup("/api/tasks").WithTags("Tasks");

            tasksApi.MapPost("/", async (CreateTaskItemDto dto, ITaskItemService taskService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var task = await taskService.CreateAsync(userId, dto);
                return Results.Ok(task);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Create a new task";
                op.Description = "Creates a new custom or suggested task for the authenticated user.";
                return op;
            });

            tasksApi.MapGet("/", async (
                ITaskItemService taskService, 
                IWellnessCheckInService wellnessService,
                IGoogleCalendarService? googleCalendarService,
                HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                
                // Get user's timezone from wellness data for filtering
                var wellnessData = await wellnessService.GetAsync(userId);
                var timezoneId = wellnessData?.TimezoneId;
                
                var dateStr = context.Request.Query["date"].FirstOrDefault();
                DateTime? date = null;
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                    date = parsedDate.Date;
                
                // Get AI-generated tasks
                var aiTasks = await taskService.GetAllAsync(userId, date, timezoneId);
                var allTasks = aiTasks.ToList();
                
                // Fetch Google Calendar events if user is connected
                if (googleCalendarService != null)
                {
                    var (isConnected, _, _) = await googleCalendarService.GetStatusAsync(userId);
                    if (isConnected)
                    {
                        // Calculate date range for Google events (same as AI tasks filter)
                        DateTime? startDate = date;
                        DateTime? endDate = date?.AddDays(1) ?? DateTime.UtcNow.AddDays(30);
                        
                        var googleEvents = await googleCalendarService.GetEventsAsync(userId, startDate, endDate);
                        
                        // Convert Google events to TaskItemDto format
                        foreach (var evt in googleEvents)
                        {
                            var duration = (int)(evt.End - evt.Start).TotalMinutes;
                            var googleTask = new TaskItemDto(
                                Id: Guid.NewGuid(), // Generate a temporary ID for Google events
                                UserId: userId,
                                Title: evt.Title,
                                Description: evt.Description ?? evt.Location,
                                Category: TaskCategory.Other,
                                OtherCategoryName: "Google Calendar",
                                Date: evt.Start.Date,
                                Time: evt.Start,
                                DurationMinutes: duration > 0 ? duration : 60, // Default to 60 minutes if all-day
                                ReminderEnabled: false,
                                RepeatType: RepeatType.Never,
                                CreatedBySuggestionEngine: false,
                                IsApproved: true,
                                Status: Models.TaskStatus.Pending,
                                ParentTaskId: null,
                                IsTemplate: false,
                                NextOccurrence: null,
                                MaxOccurrences: null,
                                EndDate: null,
                                IsActive: true,
                                SubSteps: null,
                                Urgency: null,
                                Importance: null,
                                PriorityScore: null,
                                Source: "Google" // Mark as Google Calendar event
                            );
                            allTasks.Add(googleTask);
                        }
                    }
                }
                
                // Sort by date/time
                allTasks = allTasks.OrderBy(t => t.Date).ThenBy(t => t.Time).ToList();
                
                // Return merged tasks array
                return Results.Ok(allTasks);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get all tasks";
                op.Description = "Retrieves all tasks for the authenticated user. Optionally filter by date (yyyy-MM-dd).";
                return op;
            });

            tasksApi.MapGet("/{taskId:guid}", async (Guid taskId, ITaskItemService taskService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var task = await taskService.GetByIdAsync(userId, taskId);
                if (task == null)
                    throw ApiExceptions.NotFound("Task not found");
                return Results.Ok(task);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get a task by ID";
                op.Description = "Retrieves a specific task by its ID for the authenticated user.";
                return op;
            });

            tasksApi.MapPut("/{taskId:guid}", async (Guid taskId, UpdateTaskItemDto dto, ITaskItemService taskService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var updated = await taskService.UpdateAsync(userId, taskId, dto);
                if (updated == null)
                    throw ApiExceptions.NotFound("Task not found");
                return Results.Ok(updated);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Update a task";
                op.Description = "Updates a specific task for the authenticated user.";
                return op;
            });

            tasksApi.MapDelete("/{taskId:guid}", async (Guid taskId, ITaskItemService taskService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var deleted = await taskService.DeleteAsync(userId, taskId);
                if (!deleted)
                    throw ApiExceptions.NotFound("Task not found");
                return Results.Ok();
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Delete a task";
                op.Description = "Deletes a specific task for the authenticated user.";
                return op;
            });

            tasksApi.MapPatch("/{taskId:guid}/status", async (Guid taskId, StatusUpdateDto dto, ITaskItemService taskService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var updated = await taskService.UpdateStatusAsync(userId, taskId, dto.Status);
                if (updated == null)
                    throw ApiExceptions.NotFound("Task not found");
                return Results.Ok(updated);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Update task status";
                op.Description = "Updates the status of a specific task for the authenticated user.";
                return op;
            });
        }
    }

}

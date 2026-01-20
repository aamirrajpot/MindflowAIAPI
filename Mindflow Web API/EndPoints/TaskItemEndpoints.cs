using Microsoft.AspNetCore.Mvc;
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
                ILoggerFactory loggerFactory,
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
                // Wrap in try-catch to ensure API still returns AI tasks if Google Calendar fails
                var logger = loggerFactory.CreateLogger("TaskItemEndpoints");
                if (googleCalendarService != null)
                {
                    try
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
                    catch (Exception ex)
                    {
                        // Log error but don't fail the API - return AI tasks only
                        logger.LogWarning(ex, "Failed to fetch Google Calendar events for user {UserId}. Returning AI tasks only.", userId);
                        // Continue execution - allTasks already contains AI tasks
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

            tasksApi.MapDelete("/all", async (ITaskItemService taskService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var deletedCount = await taskService.DeleteAllAsync(userId);
                return Results.Ok(new { message = $"Successfully deleted {deletedCount} task(s)", deletedCount });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Delete all tasks";
                op.Description = "Deletes all tasks for the authenticated user. Returns the count of deleted tasks.";
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

            tasksApi.MapGet("/dates", async (
                [FromQuery] string timeZoneId,
                [FromQuery] DateTime date,
                ITaskItemService taskService,
                IGoogleCalendarService? googleCalendarService,
                ILoggerFactory loggerFactory,
                HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                if (string.IsNullOrWhiteSpace(timeZoneId))
                    throw ApiExceptions.BadRequest("timeZoneId is required");

                try
                {
                    // Get timezone info
                    TimeZoneInfo timeZone;
                    try
                    {
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        // Map IANA to Windows timezone IDs
                        var windowsId = timeZoneId switch
                        {
                            "America/Chicago" => "Central Standard Time",
                            "America/New_York" => "Eastern Standard Time",
                            "America/Denver" => "Mountain Standard Time",
                            "America/Los_Angeles" => "Pacific Standard Time",
                            "America/Phoenix" => "US Mountain Standard Time",
                            _ => timeZoneId
                        };
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                    }

                    // Calculate month start and end dates in the user's timezone
                    var monthStart = new DateTime(date.Year, date.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                    // Convert month boundaries to UTC for querying
                    var monthStartUtc = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(monthStart.Date, DateTimeKind.Unspecified), timeZone);
                    var monthEndUtc = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(monthEnd.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified), timeZone);

                    // Get all tasks for the user within the month range
                    var allTasks = await taskService.GetAllAsync(userId, null, null);
                    var tasksInMonth = allTasks
                        .Where(t => t.Time >= monthStartUtc && t.Time <= monthEndUtc)
                        .ToList();

                    // Also fetch Google Calendar events if user is connected
                    var logger = loggerFactory.CreateLogger("TaskItemEndpoints");
                    if (googleCalendarService != null)
                    {
                        try
                        {
                            var (isConnected, _, _) = await googleCalendarService.GetStatusAsync(userId);
                            if (isConnected)
                            {
                                // Pass UTC dates to GetEventsAsync
                                var googleEvents = await googleCalendarService.GetEventsAsync(userId, monthStartUtc, monthEndUtc);
                                
                                // Filter Google events to ensure they fall within the month range and add to the list
                                foreach (var evt in googleEvents.Where(e => e.Start >= monthStartUtc && e.Start <= monthEndUtc))
                                {
                                    tasksInMonth.Add(new TaskItemDto(
                                        Id: Guid.NewGuid(),
                                        UserId: userId,
                                        Title: evt.Title,
                                        Description: evt.Description ?? evt.Location,
                                        Category: TaskCategory.Other,
                                        OtherCategoryName: "Google Calendar",
                                        Date: evt.Start.Date,
                                        Time: evt.Start,
                                        DurationMinutes: (int)(evt.End - evt.Start).TotalMinutes,
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
                                        Source: "Google"
                                    ));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to fetch Google Calendar events for user {UserId}", userId);
                        }
                    }

                    // Extract unique dates based on the logical task date stored in the database.
                    // We intentionally use the TaskItem.Date field here instead of converting the
                    // UTC Time into local time. This ensures that tasks scheduled for a given
                    // calendar day (e.g. 2026-01-24) are associated with that day even if their
                    // UTC time falls just outside the local day's 00:00–23:59 range.
                    var uniqueDates = tasksInMonth
                        .Select(t => t.Date.Date)
                        .Where(d => d >= monthStart && d <= monthEnd) // Ensure date is within the month
                        .Distinct()
                        .OrderBy(d => d)
                        .Select(d => d.ToString("yyyy-MM-dd"))
                        .ToList();

                    return Results.Ok(uniqueDates);
                }
                catch (Exception ex)
                {
                    var logger = loggerFactory.CreateLogger("TaskItemEndpoints");
                    logger.LogError(ex, "Error getting unique task dates for user {UserId}", userId);
                    throw ApiExceptions.InternalServerError("Failed to retrieve task dates");
                }
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get unique dates with tasks for a month";
                op.Description = "Returns unique dates (in yyyy-MM-dd format) that have tasks for the month of the specified date, considering the provided timezone.";
                return op;
            });
        }
    }

}

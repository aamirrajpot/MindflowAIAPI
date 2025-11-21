using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public class TaskItemService : ITaskItemService
    {
		private readonly MindflowDbContext _dbContext;
		private readonly ILogger<TaskItemService> _logger;
		private readonly IWellnessCheckInService _wellnessService;

		public TaskItemService(MindflowDbContext dbContext, ILogger<TaskItemService> logger, IWellnessCheckInService wellnessService)
		{
			_dbContext = dbContext;
			_logger = logger;
			_wellnessService = wellnessService;
		}

        public async Task<TaskItemDto> CreateAsync(Guid userId, CreateTaskItemDto dto)
        {
            var task = new TaskItem
            {
                UserId = userId,
                Title = dto.Title,
                Description = dto.Description,
                Category = dto.Category,
                OtherCategoryName = dto.OtherCategoryName,
                Date = dto.Date,
                Time = EnsureUtc(dto.Time),
                DurationMinutes = dto.DurationMinutes,
                ReminderEnabled = dto.ReminderEnabled,
                RepeatType = dto.RepeatType,
                CreatedBySuggestionEngine = false,
                IsApproved = true,
                Status = dto.Status,
                // Recurring task fields
                ParentTaskId = dto.ParentTaskId,
                IsTemplate = dto.IsTemplate,
                NextOccurrence = dto.NextOccurrence,
                MaxOccurrences = dto.MaxOccurrences,
                EndDate = dto.EndDate,
                IsActive = dto.IsActive
            };

            await _dbContext.Tasks.AddAsync(task);
            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        public async Task<TaskItemDto?> GetByIdAsync(Guid userId, Guid taskId)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            return task == null ? null : ToDto(task);
        }

        public async Task<IEnumerable<TaskItemDto>> GetAllAsync(Guid userId, DateTime? date = null, string? timezoneId = null)
        {
            List<TaskItem> tasks;
            
            if (date.HasValue)
            {
                // Convert the user's local date to UTC date range for filtering
                // Since tasks are stored in UTC, we need to find all tasks that fall within
                // the user's local date when converted to their timezone
                if (!string.IsNullOrWhiteSpace(timezoneId))
                {
                    try
                    {
                        // Get timezone info
                        TimeZoneInfo timeZone;
                        try
                        {
                            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                        }
                        catch (TimeZoneNotFoundException)
                        {
                            // Map IANA to Windows timezone IDs
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

                        // Convert local date start (00:00:00) to UTC
                        var localDateStart = date.Value.Date;
                        var localDateTimeStart = DateTime.SpecifyKind(localDateStart, DateTimeKind.Unspecified);
                        var utcDateStart = TimeZoneInfo.ConvertTimeToUtc(localDateTimeStart, timeZone);

                        // Convert local date end (23:59:59.999) to UTC
                        var localDateEnd = date.Value.Date.AddDays(1).AddTicks(-1);
                        var localDateTimeEnd = DateTime.SpecifyKind(localDateEnd, DateTimeKind.Unspecified);
                        var utcDateEnd = TimeZoneInfo.ConvertTimeToUtc(localDateTimeEnd, timeZone);

                        // Filter tasks where Time (UTC) falls within the UTC date range
                        tasks = await _dbContext.Tasks
                            .Where(t => t.UserId == userId 
                                && t.Time >= utcDateStart 
                                && t.Time <= utcDateEnd)
                            .OrderBy(t => t.Time)
                            .ToListAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to convert date using timezone {Timezone}, falling back to UTC date comparison", timezoneId);
                        // Fallback to simple UTC date comparison
                        tasks = await _dbContext.Tasks
                            .FromSqlRaw(@"
                                SELECT * FROM Tasks 
                                WHERE UserId = {0} 
                                AND DATE(Date) = DATE({1})
                                ORDER BY Time ASC", userId, date.Value)
                            .ToListAsync();
                    }
                }
                else
                {
                    // No timezone provided, use simple UTC date comparison
                    tasks = await _dbContext.Tasks
                        .FromSqlRaw(@"
                            SELECT * FROM Tasks 
                            WHERE UserId = {0} 
                            AND DATE(Date) = DATE({1})
                            ORDER BY Time ASC", userId, date.Value)
                        .ToListAsync();
                }
            }
            else
            {
                tasks = await _dbContext.Tasks
                    .Where(t => t.UserId == userId)
                    .OrderBy(t => t.Time)
                    .ToListAsync();
            }
            
            return tasks.Select(ToDto);
        }

        public async Task<TaskItemDto?> UpdateAsync(Guid userId, Guid taskId, UpdateTaskItemDto dto)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return null;

            if (dto.Title != null) task.Title = dto.Title;
            if (dto.Description != null) task.Description = dto.Description;
            if (dto.Category.HasValue) task.Category = dto.Category.Value;
            if (dto.OtherCategoryName != null) task.OtherCategoryName = dto.OtherCategoryName;
            if (dto.Date.HasValue) task.Date = dto.Date.Value;
            if (dto.Time.HasValue) 
            {
                task.Time = EnsureUtc(dto.Time.Value);
            }
            if (dto.DurationMinutes.HasValue) task.DurationMinutes = dto.DurationMinutes.Value;
            if (dto.ReminderEnabled.HasValue) task.ReminderEnabled = dto.ReminderEnabled.Value;
            if (dto.RepeatType.HasValue) task.RepeatType = dto.RepeatType.Value;
            if (dto.IsApproved.HasValue) task.IsApproved = dto.IsApproved.Value;
            if (dto.Status.HasValue) task.Status = dto.Status.Value;
            
            // Recurring task fields
            if (dto.ParentTaskId.HasValue) task.ParentTaskId = dto.ParentTaskId.Value;
            if (dto.IsTemplate.HasValue) task.IsTemplate = dto.IsTemplate.Value;
            if (dto.NextOccurrence.HasValue) task.NextOccurrence = dto.NextOccurrence.Value;
            if (dto.MaxOccurrences.HasValue) task.MaxOccurrences = dto.MaxOccurrences.Value;
            if (dto.EndDate.HasValue) task.EndDate = dto.EndDate.Value;
            if (dto.IsActive.HasValue) task.IsActive = dto.IsActive.Value;

            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid taskId)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return false;
            _dbContext.Tasks.Remove(task);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<TaskItemDto?> UpdateStatusAsync(Guid userId, Guid taskId, Mindflow_Web_API.Models.TaskStatus status)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return null;
            task.Status = status;
            await _dbContext.SaveChangesAsync();
            return ToDto(task);
        }

        private static TaskItemDto ToDto(TaskItem task)
        {
            return new TaskItemDto(
                task.Id,
                task.UserId,
                task.Title,
                task.Description,
                task.Category,
                task.OtherCategoryName,
                task.Date,
                EnsureUtc(task.Time),
                task.DurationMinutes,
                task.ReminderEnabled,
                task.RepeatType,
                task.CreatedBySuggestionEngine,
                task.IsApproved,
                task.Status,
                // Recurring task fields
                task.ParentTaskId,
                task.IsTemplate,
                task.NextOccurrence,
                task.MaxOccurrences,
                task.EndDate,
                task.IsActive
            );
        }

        private static DateTime EnsureUtc(DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) 
                : dateTime.ToUniversalTime();
        }

        // Recurring task methods
        public async Task<IEnumerable<TaskItemDto>> GetTasksWithRecurringAsync(Guid userId, DateTime date)
        {
            // Get user's timezone from wellness data for proper date filtering
            var wellnessData = await _wellnessService.GetAsync(userId);
            var timezoneId = wellnessData?.TimezoneId;
            
            // 1. Get existing tasks for the date (with timezone-aware filtering)
            var existingTasks = await GetAllAsync(userId, date, timezoneId);
            
            // 2. Get active templates that should have instances for this date
            var templates = await GetActiveTemplatesAsync(userId);
            
            // 3. Generate missing instances
            var newInstances = new List<TaskItem>();
            foreach (var template in templates)
            {
                if (ShouldGenerateInstanceForDate(template, date) && 
                    !await HasInstanceForDateAsync(template.Id, date))
                {
                    var instance = await GenerateTaskInstanceAsync(template.Id, date);
                    newInstances.Add(await GetTaskItemByIdAsync(instance.Id));
                }
            }
            
            // 4. Return all tasks (existing + new instances)
            return existingTasks.Concat(newInstances.Select(ToDto));
        }

        public async Task<IEnumerable<TaskItemDto>> GetActiveTemplatesAsync(Guid userId)
        {
            var templates = await _dbContext.Tasks
                .Where(t => t.UserId == userId && t.IsTemplate && t.IsActive)
                .ToListAsync();
            
            return templates.Select(ToDto);
        }

        public async Task<bool> HasInstanceForDateAsync(Guid templateId, DateTime date)
        {
            // Check if an instance already exists for this date
            var hasInstance = await _dbContext.Tasks
                .AnyAsync(t => t.ParentTaskId == templateId && t.Date.Date == date.Date);
            
            if (hasInstance)
                return true;
            
            // Also check if the template itself is scheduled for this date
            // (templates can be scheduled for their original date)
            var template = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == templateId);
            if (template != null && template.Date.Date == date.Date)
                return true;
            
            return false;
        }

		public async Task<TaskItemDto> GenerateTaskInstanceAsync(Guid templateId, DateTime date)
		{
			var template = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == templateId);
			if (template == null)
				throw new ArgumentException("Template not found");

			var wellness = await _wellnessService.GetAsync(template.UserId);
			var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
			var scheduledTime = DetermineSmartRecurringTime(date, template.Time.TimeOfDay, template.DurationMinutes, isWeekend, wellness);

			var instance = new TaskItem
			{
				UserId = template.UserId,
				Title = template.Title,
				Description = template.Description,
				Category = template.Category,
				OtherCategoryName = template.OtherCategoryName,
				Date = date,
				Time = scheduledTime,
				DurationMinutes = template.DurationMinutes,
				ReminderEnabled = template.ReminderEnabled,
				RepeatType = RepeatType.Never, // Instances don't repeat
				CreatedBySuggestionEngine = template.CreatedBySuggestionEngine,
				IsApproved = template.IsApproved,
				Status = Models.TaskStatus.Pending,
				// Recurring task fields
				ParentTaskId = templateId,
				IsTemplate = false,
				IsActive = true
			};

			_dbContext.Tasks.Add(instance);
			await _dbContext.SaveChangesAsync();
			
			return ToDto(instance);
		}

        private async Task<TaskItem> GetTaskItemByIdAsync(Guid taskId)
        {
            var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new ArgumentException("Task not found");
            return task;
        }

		private static bool ShouldGenerateInstanceForDate(TaskItemDto template, DateTime date)
        {
            // Check if template should generate an instance for this date
            if (!template.IsTemplate || !template.IsActive)
                return false;

            // Check if date is within the template's date range
            if (template.EndDate.HasValue && date > template.EndDate.Value)
                return false;

            // For recurring tasks, don't generate an instance for the template's original date
            // (the template itself covers that date)
            if (date.Date == template.Date.Date)
                return false;

            // Check if we've exceeded max occurrences
            if (template.MaxOccurrences.HasValue)
            {
                // This would require counting existing instances - simplified for now
                // In a full implementation, you'd count existing instances
            }

            // Check if the date matches the recurrence pattern
            return template.RepeatType switch
            {
                RepeatType.Day => true, // Daily - generate for all dates after template date
                RepeatType.Week => date.DayOfWeek == template.Date.DayOfWeek && date.Date > template.Date.Date, // Same day of week, after template date
                RepeatType.Month => date.Day == template.Date.Day && date.Date > template.Date.Date, // Same day of month, after template date
                _ => false
            };
        }

		private static (TimeSpan start, TimeSpan end)? ExtractSlot(DateTime? startUtc, DateTime? endUtc)
		{
			if (!startUtc.HasValue || !endUtc.HasValue)
			{
				return null;
			}

			var start = startUtc.Value.TimeOfDay;
			var end = endUtc.Value.TimeOfDay;

			// If slot crosses midnight, fallback to null (smart scheduling handles only same-day windows here)
			if (start >= end)
			{
				return null;
			}

			return (start, end);
		}

		private DateTime DetermineSmartRecurringTime(DateTime date, TimeSpan desiredTime, int durationMinutes, bool isWeekend, WellnessCheckInDto? wellness)
		{
			// Use UTC fields if available (they're already converted to UTC)
			// Otherwise fall back to converting local time fields
			(TimeSpan start, TimeSpan end)? slots = null;
			if (wellness != null)
			{
				// Prioritize UTC fields first (they're already converted to UTC)
				if (isWeekend && wellness.WeekendStartTimeUtc.HasValue && wellness.WeekendEndTimeUtc.HasValue)
				{
					slots = (wellness.WeekendStartTimeUtc.Value.TimeOfDay, wellness.WeekendEndTimeUtc.Value.TimeOfDay);
				}
				else if (!isWeekend && wellness.WeekdayStartTimeUtc.HasValue && wellness.WeekdayEndTimeUtc.HasValue)
				{
					slots = (wellness.WeekdayStartTimeUtc.Value.TimeOfDay, wellness.WeekdayEndTimeUtc.Value.TimeOfDay);
				}
				else if (!string.IsNullOrWhiteSpace(wellness.TimezoneId))
				{
					// Fall back to converting local time fields
					if (isWeekend)
					{
						slots = ParseTimeSlotsForDate(
							wellness.WeekendStartTime,
							wellness.WeekendStartShift,
							wellness.WeekendEndTime,
							wellness.WeekendEndShift,
							date,
							wellness.TimezoneId);
					}
					else
					{
						slots = ParseTimeSlotsForDate(
							wellness.WeekdayStartTime,
							wellness.WeekdayStartShift,
							wellness.WeekdayEndTime,
							wellness.WeekdayEndShift,
							date,
							wellness.TimezoneId);
					}
				}
			}

			if (slots == null)
			{
				return date.Date.Add(desiredTime);
			}

			var start = slots.Value.start;
			var end = slots.Value.end;
			var duration = TimeSpan.FromMinutes(durationMinutes <= 0 ? 30 : durationMinutes);
			var scheduled = desiredTime;

			if (scheduled < start || scheduled + duration > end)
			{
				scheduled = start;
				if (scheduled + duration > end)
				{
					scheduled = start;
				}
			}

			var startDateTime = DateTime.SpecifyKind(date.Date.Add(scheduled), DateTimeKind.Utc);
			if (IsSlotOccupied(templateUserId: wellness?.UserId ?? Guid.Empty, startDateTime, durationMinutes))
			{
				var alternate = FindNextAvailableWithinSlot(date, start, end, scheduled, duration, templateUserId: wellness?.UserId ?? Guid.Empty, durationMinutes);
				return alternate ?? startDateTime;
			}

			return startDateTime;
		}

		/// <summary>
		/// Parses time slots from local time strings and converts them to UTC TimeSpan for a specific target date.
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
				return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0)); // Default
			}

			// Parse local time strings to TimeSpan
			var localStartTime = ParseTimeString(startTime, startShift);
			var localEndTime = ParseTimeString(endTime, endShift);

			// Convert local time to UTC for the target date
			var startUtc = ConvertLocalTimeToUtcForDate(localStartTime, targetDate, timezoneId);
			var endUtc = ConvertLocalTimeToUtcForDate(localEndTime, targetDate, timezoneId);

			// Handle day boundary crossing
			if (startUtc >= endUtc)
			{
				var isDayBoundaryCrossing = (localStartTime.TotalMinutes >= 22 * 60 || localEndTime.TotalMinutes <= 2 * 60);
				if (!isDayBoundaryCrossing)
				{
					return (new TimeSpan(19, 0, 0), new TimeSpan(22, 0, 0)); // Default
				}
			}

			return (startUtc, endUtc);
		}

		/// <summary>
		/// Parses a time string with optional AM/PM shift to TimeSpan.
		/// </summary>
		private static TimeSpan ParseTimeString(string? timeStr, string? shift)
		{
			if (string.IsNullOrWhiteSpace(timeStr))
				return new TimeSpan(9, 0, 0); // Default 9 AM

			if (TimeSpan.TryParse(timeStr, out var time))
			{
				if (!string.IsNullOrWhiteSpace(shift))
				{
					var shiftUpper = shift.ToUpper();
					var isPM = shiftUpper.Contains("PM");
					var isAM = shiftUpper.Contains("AM");

					if (isPM && time.Hours >= 1 && time.Hours <= 12)
					{
						time = time.Add(new TimeSpan(12, 0, 0));
					}
					else if (isAM && time.Hours == 12)
					{
						time = time.Subtract(new TimeSpan(12, 0, 0));
					}
				}
				return time;
			}

			return new TimeSpan(9, 0, 0); // Default fallback
		}

		/// <summary>
		/// Converts a local time (TimeSpan) to UTC TimeSpan for a specific date using the timezone ID.
		/// </summary>
		private static TimeSpan ConvertLocalTimeToUtcForDate(TimeSpan localTime, DateTime targetDate, string? timezoneId)
		{
			if (string.IsNullOrWhiteSpace(timezoneId))
			{
				return localTime; // Assume already UTC
			}

			try
			{
				TimeZoneInfo timeZone;
				try
				{
					timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
				}
				catch (TimeZoneNotFoundException)
				{
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

				var localDateTime = targetDate.Date.Add(localTime);
				var localDateTimeUnspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
				var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTimeUnspecified, timeZone);
				utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

				return utcDateTime.TimeOfDay;
			}
			catch
			{
				return localTime; // Fallback: assume already UTC
			}
		}

		private bool IsSlotOccupied(Guid templateUserId, DateTime start, int durationMinutes)
		{
			if (templateUserId == Guid.Empty)
			{
				return false;
			}

			var end = start.AddMinutes(durationMinutes);
			return _dbContext.Tasks.Any(t => t.UserId == templateUserId && t.IsActive && t.Time < end && t.Time.AddMinutes(t.DurationMinutes) > start);
		}

		private DateTime? FindNextAvailableWithinSlot(DateTime date, TimeSpan slotStart, TimeSpan slotEnd, TimeSpan current, TimeSpan duration, Guid templateUserId, int durationMinutes)
		{
			var start = DateTime.SpecifyKind(date.Date.Add(current), DateTimeKind.Utc);
			var maxIterations = (int)((slotEnd - slotStart).TotalMinutes / 30);
			var iterator = 0;
			var cursor = current.Add(TimeSpan.FromMinutes(30));

			while (cursor.Add(duration) <= slotEnd && iterator < maxIterations)
			{
				var candidate = DateTime.SpecifyKind(date.Date.Add(cursor), DateTimeKind.Utc);
				if (!IsSlotOccupied(templateUserId, candidate, durationMinutes))
				{
					return candidate;
				}

				cursor = cursor.Add(TimeSpan.FromMinutes(30));
				iterator++;
			}

			return null;
		}
    }
}

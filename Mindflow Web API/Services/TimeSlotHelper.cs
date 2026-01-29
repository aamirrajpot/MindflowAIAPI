using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Helper class for managing time slots that may cross midnight.
    /// All times are in UTC.
    /// </summary>
    public class TimeSlotHelper
    {
        private readonly ILogger<TimeSlotHelper>? _logger;

        public TimeSlotHelper(ILogger<TimeSlotHelper>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Represents a time slot that may cross midnight.
        /// All times are in UTC.
        /// </summary>
        public class UtcTimeSlot
        {
            public int StartMinutesFromMidnight { get; set; }  // 0-1439 for same day
            public int EndMinutesFromMidnight { get; set; }    // Can be > 1440 if crosses midnight

            public bool CrossesMidnight => EndMinutesFromMidnight > 1440 || EndMinutesFromMidnight < StartMinutesFromMidnight;

            public TimeSpan StartTime => TimeSpan.FromMinutes(StartMinutesFromMidnight % 1440);
            public TimeSpan EndTime => TimeSpan.FromMinutes(EndMinutesFromMidnight % 1440);

            /// <summary>
            /// For a given UTC date, get the actual UTC DateTime range this slot covers
            /// </summary>
            public (DateTime start, DateTime end) GetUtcRangeForDate(DateTime utcDate)
            {
                utcDate = DateTime.SpecifyKind(utcDate.Date, DateTimeKind.Utc);

                var start = utcDate.AddMinutes(StartMinutesFromMidnight);
                var end = utcDate.AddMinutes(EndMinutesFromMidnight);

                return (start, end);
            }
        }

        /// <summary>
        /// Convert user's local time input to UTC minutes from midnight
        /// </summary>
        public int ConvertLocalTimeToUtcMinutes(string? time, string? shift, DateTime referenceDate, string? timezoneId)
        {
            if (string.IsNullOrEmpty(time) || string.IsNullOrEmpty(shift) || string.IsNullOrEmpty(timezoneId))
                return 0;

            // Parse the time string (e.g., "09:30" or "9:30")
            var parts = time.Split(':');
            if (parts.Length != 2) return 0;

            if (!int.TryParse(parts[0], out int hours) || !int.TryParse(parts[1], out int minutes))
                return 0;

            // Convert to 24-hour format
            if (shift.Equals("PM", StringComparison.OrdinalIgnoreCase) && hours != 12)
                hours += 12;
            else if (shift.Equals("AM", StringComparison.OrdinalIgnoreCase) && hours == 12)
                hours = 0;

            // Create local DateTime for the user's timezone
            TimeZoneInfo userTimeZone;
            try
            {
                userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
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
                try
                {
                    userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    _logger?.LogWarning("Invalid timezone ID: {TimezoneId}, using UTC", timezoneId);
                    return hours * 60 + minutes;
                }
            }

            // Create a local time on the reference date
            var localDateTime = new DateTime(referenceDate.Year, referenceDate.Month, referenceDate.Day,
                hours, minutes, 0, DateTimeKind.Unspecified);

            // Convert to UTC
            var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, userTimeZone);

            // Calculate minutes from midnight UTC
            var minutesFromMidnight = (int)utcDateTime.TimeOfDay.TotalMinutes;

            // If conversion pushed us to next/previous day, adjust accordingly
            if (utcDateTime.Date > referenceDate.Date)
            {
                minutesFromMidnight += 1440; // Add a day's worth of minutes
            }
            else if (utcDateTime.Date < referenceDate.Date)
            {
                minutesFromMidnight -= 1440; // Subtract a day
            }

            return minutesFromMidnight;
        }

        /// <summary>
        /// Get UTC time slot for a specific date (handles both weekday and weekend)
        /// </summary>
        public UtcTimeSlot? GetUtcSlotForDate(DateTime utcDate, WellnessCheckInDto wellness)
        {
            var isWeekend = utcDate.DayOfWeek == DayOfWeek.Saturday || utcDate.DayOfWeek == DayOfWeek.Sunday;

            int? startMinutes, endMinutes;

            if (isWeekend)
            {
                startMinutes = wellness.WeekendStartMinutesUtc;
                endMinutes = wellness.WeekendEndMinutesUtc;
            }
            else
            {
                startMinutes = wellness.WeekdayStartMinutesUtc;
                endMinutes = wellness.WeekdayEndMinutesUtc;
            }

            if (!startMinutes.HasValue || !endMinutes.HasValue)
                return null;

            return new UtcTimeSlot
            {
                StartMinutesFromMidnight = startMinutes.Value,
                EndMinutesFromMidnight = endMinutes.Value
            };
        }

        /// <summary>
        /// Get UTC time slot for a specific date from WellnessCheckIn entity
        /// </summary>
        public UtcTimeSlot? GetUtcSlotForDate(DateTime utcDate, WellnessCheckIn wellness)
        {
            var isWeekend = utcDate.DayOfWeek == DayOfWeek.Saturday || utcDate.DayOfWeek == DayOfWeek.Sunday;

            int? startMinutes, endMinutes;

            if (isWeekend)
            {
                startMinutes = wellness.WeekendStartMinutesUtc;
                endMinutes = wellness.WeekendEndMinutesUtc;
            }
            else
            {
                startMinutes = wellness.WeekdayStartMinutesUtc;
                endMinutes = wellness.WeekdayEndMinutesUtc;
            }

            if (!startMinutes.HasValue || !endMinutes.HasValue)
                return null;

            return new UtcTimeSlot
            {
                StartMinutesFromMidnight = startMinutes.Value,
                EndMinutesFromMidnight = endMinutes.Value
            };
        }
    }

    /// <summary>
    /// Processes wellness data to compute UTC minute offsets
    /// </summary>
    public class WellnessDataProcessor
    {
        private readonly TimeSlotHelper _timeSlotHelper;
        private readonly ILogger<WellnessDataProcessor>? _logger;

        public WellnessDataProcessor(TimeSlotHelper timeSlotHelper, ILogger<WellnessDataProcessor>? logger = null)
        {
            _timeSlotHelper = timeSlotHelper;
            _logger = logger;
        }

        /// <summary>
        /// Call this when saving wellness data to pre-compute UTC offsets
        /// </summary>
        public void ComputeUtcOffsets(WellnessCheckIn wellness)
        {
            if (string.IsNullOrEmpty(wellness.TimezoneId))
            {
                _logger?.LogWarning("No timezone provided for wellness data");
                return;
            }

            // Use a reference date (doesn't matter which, we just need a date to anchor the conversion)
            // We'll use a date far enough in the future to avoid DST issues
            var referenceDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);

            // Weekday slots
            if (!string.IsNullOrEmpty(wellness.WeekdayStartTime) && !string.IsNullOrEmpty(wellness.WeekdayStartShift))
            {
                wellness.WeekdayStartMinutesUtc = _timeSlotHelper.ConvertLocalTimeToUtcMinutes(
                    wellness.WeekdayStartTime, wellness.WeekdayStartShift, referenceDate, wellness.TimezoneId);
            }

            if (!string.IsNullOrEmpty(wellness.WeekdayEndTime) && !string.IsNullOrEmpty(wellness.WeekdayEndShift))
            {
                var endMinutes = _timeSlotHelper.ConvertLocalTimeToUtcMinutes(
                    wellness.WeekdayEndTime, wellness.WeekdayEndShift, referenceDate, wellness.TimezoneId);

                // If end appears before start in UTC, it means it crosses midnight
                if (wellness.WeekdayStartMinutesUtc.HasValue && endMinutes < wellness.WeekdayStartMinutesUtc.Value)
                {
                    endMinutes += 1440; // Add 24 hours
                }

                wellness.WeekdayEndMinutesUtc = endMinutes;
            }

            // Weekend slots
            if (!string.IsNullOrEmpty(wellness.WeekendStartTime) && !string.IsNullOrEmpty(wellness.WeekendStartShift))
            {
                wellness.WeekendStartMinutesUtc = _timeSlotHelper.ConvertLocalTimeToUtcMinutes(
                    wellness.WeekendStartTime, wellness.WeekendStartShift, referenceDate, wellness.TimezoneId);
            }

            if (!string.IsNullOrEmpty(wellness.WeekendEndTime) && !string.IsNullOrEmpty(wellness.WeekendEndShift))
            {
                var endMinutes = _timeSlotHelper.ConvertLocalTimeToUtcMinutes(
                    wellness.WeekendEndTime, wellness.WeekendEndShift, referenceDate, wellness.TimezoneId);

                if (wellness.WeekendStartMinutesUtc.HasValue && endMinutes < wellness.WeekendStartMinutesUtc.Value)
                {
                    endMinutes += 1440;
                }

                wellness.WeekendEndMinutesUtc = endMinutes;
            }

            _logger?.LogInformation("Computed UTC offsets for timezone {Timezone}: Weekday [{Start}-{End}], Weekend [{WStart}-{WEnd}]",
                wellness.TimezoneId,
                wellness.WeekdayStartMinutesUtc,
                wellness.WeekdayEndMinutesUtc,
                wellness.WeekendStartMinutesUtc,
                wellness.WeekendEndMinutesUtc);
        }
    }
}


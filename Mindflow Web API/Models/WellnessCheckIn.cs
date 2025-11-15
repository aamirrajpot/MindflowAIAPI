using System;
using System.Collections.Generic;
using System.Text.Json;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Models
{
    public class WellnessCheckIn : EntityBase
    {
        public Guid UserId { get; set; }
        public string MoodLevel { get; set; } = string.Empty;  // Required field
        public DateTime CheckInDate { get; set; }
        public bool ReminderEnabled { get; set; }
        public string? ReminderTime { get; set; }
        
        // Core fixed fields (always asked)
        public string? AgeRange { get; set; }         // 'Under 18', '18-24', '25-34', '35-44', '45-54', '55+'
        public string[]? FocusAreas { get; set; }     // Array of selected focus areas (max 3)
        
        // Time availability fields (always asked) - stored as user entered (local time)
        public string? WeekdayStartTime { get; set; }      // Weekday start time (e.g., "09:30")
        public string? WeekdayStartShift { get; set; }     // Weekday start AM/PM
        public string? WeekdayEndTime { get; set; }        // Weekday end time (e.g., "17:30")
        public string? WeekdayEndShift { get; set; }       // Weekday end AM/PM
        public string? WeekendStartTime { get; set; }      // Weekend start time (e.g., "10:00")
        public string? WeekendStartShift { get; set; }     // Weekend start AM/PM
        public string? WeekendEndTime { get; set; }        // Weekend end time (e.g., "18:00")
        public string? WeekendEndShift { get; set; }       // Weekend end AM/PM
        
        // UTC time fields (for backend processing) - stored in 24-hour format
        public string? WeekdayStartTimeUtc { get; set; }   // Weekday start time in UTC (e.g., "19:00")
        public string? WeekdayEndTimeUtc { get; set; }     // Weekday end time in UTC (e.g., "22:00")
        public string? WeekendStartTimeUtc { get; set; }   // Weekend start time in UTC (e.g., "15:30")
        public string? WeekendEndTimeUtc { get; set; }     // Weekend end time in UTC (e.g., "21:00")
        
        // Dynamic questions/answers stored as JSON
        // Stores all conditional questions based on selected focus areas
        // Key: question name (e.g., "mentalHealthChallenges", "productivityBlockers", "biggestObstacle", "supportNeeds")
        // Value: string for single/text answers, string[] for multiple choice
        // Examples:
        //   - "mentalHealthChallenges": ["Anxiety", "Stress", "Burnout"]
        //   - "mentalHealthSupport": "therapist_regular"
        //   - "mentalHealthTriggers": "Work deadlines and social situations"
        //   - "biggestObstacle": "Lack of motivation and time management"
        //   - "supportNeeds": ["Daily check-ins", "Task suggestions", "AI insights"]
        public Dictionary<string, object> Questions { get; set; } = new Dictionary<string, object>();

        // Private constructor for ORM frameworks
        private WellnessCheckIn()
        {
            MoodLevel = string.Empty;
        }

        private WellnessCheckIn(Guid userId, string moodLevel, DateTime checkInDate, bool reminderEnabled, string? reminderTime, string? ageRange, string[]? focusAreas, string? weekdayStartTime, string? weekdayStartShift, string? weekdayEndTime, string? weekdayEndShift, string? weekendStartTime, string? weekendStartShift, string? weekendEndTime, string? weekendEndShift, string? weekdayStartTimeUtc, string? weekdayEndTimeUtc, string? weekendStartTimeUtc, string? weekendEndTimeUtc, Dictionary<string, object> questions)
        {
            UserId = userId;
            MoodLevel = moodLevel;
            CheckInDate = checkInDate;
            ReminderEnabled = reminderEnabled;
            ReminderTime = reminderTime;
            AgeRange = ageRange;
            FocusAreas = focusAreas;
            WeekdayStartTime = weekdayStartTime;
            WeekdayStartShift = weekdayStartShift;
            WeekdayEndTime = weekdayEndTime;
            WeekdayEndShift = weekdayEndShift;
            WeekendStartTime = weekendStartTime;
            WeekendStartShift = weekendStartShift;
            WeekendEndTime = weekendEndTime;
            WeekendEndShift = weekendEndShift;
            WeekdayStartTimeUtc = weekdayStartTimeUtc;
            WeekdayEndTimeUtc = weekdayEndTimeUtc;
            WeekendStartTimeUtc = weekendStartTimeUtc;
            WeekendEndTimeUtc = weekendEndTimeUtc;
            Questions = questions ?? new Dictionary<string, object>();
        }

        public static WellnessCheckIn Create(Guid userId, string moodLevel, DateTime checkInDate, bool reminderEnabled = false, string? reminderTime = null, string? ageRange = null, string[]? focusAreas = null, string? weekdayStartTime = null, string? weekdayStartShift = null, string? weekdayEndTime = null, string? weekdayEndShift = null, string? weekendStartTime = null, string? weekendStartShift = null, string? weekendEndTime = null, string? weekendEndShift = null, string? weekdayStartTimeUtc = null, string? weekdayEndTimeUtc = null, string? weekendStartTimeUtc = null, string? weekendEndTimeUtc = null, Dictionary<string, object>? questions = null)
        {
            ValidateInputs(moodLevel, checkInDate, ageRange, focusAreas, weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift, questions);
            return new WellnessCheckIn(userId, moodLevel, checkInDate, reminderEnabled, reminderTime, ageRange, focusAreas, weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift, weekdayStartTimeUtc, weekdayEndTimeUtc, weekendStartTimeUtc, weekendEndTimeUtc, questions ?? new Dictionary<string, object>());
        }

        public void Update(string moodLevel, DateTime checkInDate, bool reminderEnabled, string? reminderTime, string? ageRange, string[]? focusAreas, string? weekdayStartTime, string? weekdayStartShift, string? weekdayEndTime, string? weekdayEndShift, string? weekendStartTime, string? weekendStartShift, string? weekendEndTime, string? weekendEndShift, string? weekdayStartTimeUtc, string? weekdayEndTimeUtc, string? weekendStartTimeUtc, string? weekendEndTimeUtc, Dictionary<string, object>? questions)
        {
            ValidateInputs(moodLevel, checkInDate, ageRange, focusAreas, weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift, questions);

            MoodLevel = moodLevel;
            CheckInDate = checkInDate;
            ReminderEnabled = reminderEnabled;
            ReminderTime = reminderTime;
            AgeRange = ageRange;
            FocusAreas = focusAreas;
            WeekdayStartTime = weekdayStartTime;
            WeekdayStartShift = weekdayStartShift;
            WeekdayEndTime = weekdayEndTime;
            WeekdayEndShift = weekdayEndShift;
            WeekendStartTime = weekendStartTime;
            WeekendStartShift = weekendStartShift;
            WeekendEndTime = weekendEndTime;
            WeekendEndShift = weekendEndShift;
            WeekdayStartTimeUtc = weekdayStartTimeUtc;
            WeekdayEndTimeUtc = weekdayEndTimeUtc;
            WeekendStartTimeUtc = weekendStartTimeUtc;
            WeekendEndTimeUtc = weekendEndTimeUtc;
            
            if (questions != null)
            {
                Questions = questions;
            }

            UpdateLastModified();
        }

        // Helper method to get a question value as a specific type
        public T? GetQuestionValue<T>(string questionKey)
        {
            if (!Questions.TryGetValue(questionKey, out var value) || value == null)
                return default(T);

            // Handle JSON deserialization for complex types
            if (value is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }

            // Direct cast for simple types
            if (value is T directValue)
                return directValue;

            // Try to convert
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }

        // Helper method to set a question value
        public void SetQuestionValue(string questionKey, object? value)
        {
            if (value == null)
            {
                Questions.Remove(questionKey);
            }
            else
            {
                Questions[questionKey] = value;
            }
            UpdateLastModified();
        }

        private static void ValidateInputs(string moodLevel, DateTime checkInDate, string? ageRange, string[]? focusAreas, string? weekdayStartTime, string? weekdayStartShift, string? weekdayEndTime, string? weekdayEndShift, string? weekendStartTime, string? weekendStartShift, string? weekendEndTime, string? weekendEndShift, Dictionary<string, object>? questions)
        {
            // Allow null or empty moodLevel, but if provided, it must be valid
            if (!string.IsNullOrWhiteSpace(moodLevel) && !MoodHelper.IsValidMood(moodLevel))
                throw new ArgumentException("Mood level must be one of: 'Shopping', 'Okay', 'Stressed', 'Overwhelmed'.", nameof(moodLevel));

            if (checkInDate > DateTime.UtcNow)
                throw new ArgumentException("Check-in date cannot be in the future.", nameof(checkInDate));

            if (!string.IsNullOrWhiteSpace(ageRange) && !AgeRangeHelper.IsValidAgeRange(ageRange))
                throw new ArgumentException("Age range must be one of: 'Under 18', '18-24', '25-34', '35-44', '45-54', '55+'.", nameof(ageRange));

            if (!FocusAreasHelper.IsValidFocusAreasList(focusAreas))
                throw new ArgumentException($"Focus areas must be valid and cannot exceed {FocusAreasHelper.MaxFocusAreas} selections.", nameof(focusAreas));

            // Validate time fields
            ValidateTimeField(weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, "Weekday");
            ValidateTimeField(weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift, "Weekend");

            // Validate dynamic questions
            if (questions != null)
            {
                foreach (var kvp in questions)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key))
                        throw new ArgumentException("Question keys cannot be null or empty.", nameof(questions));

                    // Validate string values length (max 2000 chars per text question)
                    if (kvp.Value is string strValue && strValue.Length > 2000)
                        throw new ArgumentException($"Question '{kvp.Key}' value cannot exceed 2000 characters.", nameof(questions));

                    // Validate array values (max 20 items per multi-select question)
                    if (kvp.Value is System.Collections.IEnumerable enumerable && !(kvp.Value is string))
                    {
                        var count = 0;
                        foreach (var item in enumerable)
                        {
                            count++;
                            if (count > 20)
                                throw new ArgumentException($"Question '{kvp.Key}' cannot have more than 20 items.", nameof(questions));
                        }
                    }
                }
            }
        }

        private static void ValidateTimeField(string? startTime, string? startShift, string? endTime, string? endShift, string timeType)
        {
            if (!string.IsNullOrWhiteSpace(startShift) && startShift != "AM" && startShift != "PM")
                throw new ArgumentException($"{timeType} start time shift must be either 'AM' or 'PM'.", nameof(startShift));

            if (!string.IsNullOrWhiteSpace(endShift) && endShift != "AM" && endShift != "PM")
                throw new ArgumentException($"{timeType} end time shift must be either 'AM' or 'PM'.", nameof(endShift));

            if (!string.IsNullOrWhiteSpace(startTime) && !IsValidTimeFormat(startTime))
                throw new ArgumentException($"{timeType} start time must be in HH:MM format (e.g., '09:30').", nameof(startTime));

            if (!string.IsNullOrWhiteSpace(endTime) && !IsValidTimeFormat(endTime))
                throw new ArgumentException($"{timeType} end time must be in HH:MM format (e.g., '17:30').", nameof(endTime));
        }

        private static bool IsValidTimeFormat(string time)
        {
            if (string.IsNullOrWhiteSpace(time)) return true;
            
            return System.Text.RegularExpressions.Regex.IsMatch(time, @"^([01]?[0-9]|2[0-3]):[0-5][0-9]$");
        }
    }
} 
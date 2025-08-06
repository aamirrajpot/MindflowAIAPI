using System;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Models
{
    public class WellnessCheckIn : EntityBase
    {
        public Guid UserId { get; set; }
        public string MoodLevel { get; set; }            // 'neutral', 'happy', 'sad'
        public DateTime CheckInDate { get; set; }
        public bool ReminderEnabled { get; set; }
        public string? ReminderTime { get; set; }
        public string? AgeRange { get; set; }         // 'Under 18', '18-24', '25-34', '35-44', '45-54', '55+'
        public string[]? FocusAreas { get; set; }     // Array of selected focus areas (max 3)
        public string? StressNotes { get; set; }      // User's stress-related notes (max 500 chars)
        public string? ThoughtTrackingMethod { get; set; }  // How user tracks thoughts/tasks
        public string[]? SupportAreas { get; set; }   // What would help feel supported (max 2)
        public string? SelfCareFrequency { get; set; } // How often they care for themselves
        public string? ToughDayMessage { get; set; }  // Message for tough days (max 500 chars)
        public string[]? CopingMechanisms { get; set; } // What helps when overwhelmed (multiple selection)
        public string? JoyPeaceSources { get; set; }  // What gives joy or peace (max 500 chars)
        
        // 4 fields for WeekdayFreeTime (replacing WeekdayFreeTime)
        public string? WeekdayStartTime { get; set; }      // Weekday start time (e.g., "09:30")
        public string? WeekdayStartShift { get; set; }     // Weekday start AM/PM
        public string? WeekdayEndTime { get; set; }        // Weekday end time (e.g., "17:30")
        public string? WeekdayEndShift { get; set; }       // Weekday end AM/PM
        
        // 4 fields for WeekendFreeTime (replacing WeekendFreeTime)
        public string? WeekendStartTime { get; set; }      // Weekend start time (e.g., "10:00")
        public string? WeekendStartShift { get; set; }     // Weekend start AM/PM
        public string? WeekendEndTime { get; set; }        // Weekend end time (e.g., "18:00")
        public string? WeekendEndShift { get; set; }       // Weekend end AM/PM

        // Private constructor for ORM frameworks
        private WellnessCheckIn()
        {
            MoodLevel = string.Empty;
        }

        private WellnessCheckIn(Guid userId, string mood, DateTime checkInDate, bool reminderEnabled, string? reminderTime, string? ageRange, string[]? focusAreas, string? stressNotes, string? thoughtTrackingMethod, string[]? supportAreas, string? selfCareFrequency, string? toughDayMessage, string[]? copingMechanisms, string? joyPeaceSources, string? weekdayStartTime, string? weekdayStartShift, string? weekdayEndTime, string? weekdayEndShift, string? weekendStartTime, string? weekendStartShift, string? weekendEndTime, string? weekendEndShift)
        {
            UserId = userId;
            MoodLevel = mood;
            CheckInDate = checkInDate;
            ReminderEnabled = reminderEnabled;
            ReminderTime = reminderTime;
            AgeRange = ageRange;
            FocusAreas = focusAreas;
            StressNotes = stressNotes;
            ThoughtTrackingMethod = thoughtTrackingMethod;
            SupportAreas = supportAreas;
            SelfCareFrequency = selfCareFrequency;
            ToughDayMessage = toughDayMessage;
            CopingMechanisms = copingMechanisms;
            JoyPeaceSources = joyPeaceSources;
            WeekdayStartTime = weekdayStartTime;
            WeekdayStartShift = weekdayStartShift;
            WeekdayEndTime = weekdayEndTime;
            WeekdayEndShift = weekdayEndShift;
            WeekendStartTime = weekendStartTime;
            WeekendStartShift = weekendStartShift;
            WeekendEndTime = weekendEndTime;
            WeekendEndShift = weekendEndShift;
        }

        public static WellnessCheckIn Create(Guid userId, string mood, DateTime checkInDate, bool reminderEnabled = false, string? reminderTime = null, string? ageRange = null, string[]? focusAreas = null, string? stressNotes = null, string? thoughtTrackingMethod = null, string[]? supportAreas = null, string? selfCareFrequency = null, string? toughDayMessage = null, string[]? copingMechanisms = null, string? joyPeaceSources = null, string? weekdayStartTime = null, string? weekdayStartShift = null, string? weekdayEndTime = null, string? weekdayEndShift = null, string? weekendStartTime = null, string? weekendStartShift = null, string? weekendEndTime = null, string? weekendEndShift = null)
        {
            ValidateInputs(mood, checkInDate, ageRange, focusAreas, stressNotes, thoughtTrackingMethod, supportAreas, selfCareFrequency, toughDayMessage, copingMechanisms, joyPeaceSources, weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift);
            return new WellnessCheckIn(userId, mood, checkInDate, reminderEnabled, reminderTime, ageRange, focusAreas, stressNotes, thoughtTrackingMethod, supportAreas, selfCareFrequency, toughDayMessage, copingMechanisms, joyPeaceSources, weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift);
        }

        public void Update(string mood, DateTime checkInDate, bool reminderEnabled, string? reminderTime, string? ageRange, string[]? focusAreas, string? stressNotes, string? thoughtTrackingMethod, string[]? supportAreas, string? selfCareFrequency, string? toughDayMessage, string[]? copingMechanisms, string? joyPeaceSources, string? weekdayStartTime, string? weekdayStartShift, string? weekdayEndTime, string? weekdayEndShift, string? weekendStartTime, string? weekendStartShift, string? weekendEndTime, string? weekendEndShift)
        {
            ValidateInputs(mood, checkInDate, ageRange, focusAreas, stressNotes, thoughtTrackingMethod, supportAreas, selfCareFrequency, toughDayMessage, copingMechanisms, joyPeaceSources, weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift);

            MoodLevel = mood;
            CheckInDate = checkInDate;
            ReminderEnabled = reminderEnabled;
            ReminderTime = reminderTime;
            AgeRange = ageRange;
            FocusAreas = focusAreas;
            StressNotes = stressNotes;
            ThoughtTrackingMethod = thoughtTrackingMethod;
            SupportAreas = supportAreas;
            SelfCareFrequency = selfCareFrequency;
            ToughDayMessage = toughDayMessage;
            CopingMechanisms = copingMechanisms;
            JoyPeaceSources = joyPeaceSources;
            WeekdayStartTime = weekdayStartTime;
            WeekdayStartShift = weekdayStartShift;
            WeekdayEndTime = weekdayEndTime;
            WeekdayEndShift = weekdayEndShift;
            WeekendStartTime = weekendStartTime;
            WeekendStartShift = weekendStartShift;
            WeekendEndTime = weekendEndTime;
            WeekendEndShift = weekendEndShift;

            UpdateLastModified();
        }

        private static void ValidateInputs(string mood, DateTime checkInDate, string? ageRange, string[]? focusAreas, string? stressNotes, string? thoughtTrackingMethod, string[]? supportAreas, string? selfCareFrequency, string? toughDayMessage, string[]? copingMechanisms, string? joyPeaceSources, string? weekdayStartTime, string? weekdayStartShift, string? weekdayEndTime, string? weekdayEndShift, string? weekendStartTime, string? weekendStartShift, string? weekendEndTime, string? weekendEndShift)
        {
            if (string.IsNullOrWhiteSpace(mood) || !MoodHelper.IsValidMood(mood))
                throw new ArgumentException("Mood level must be one of: 'Shopping', 'Okay', 'Stressed', 'Overwhelmed'.", nameof(mood));

            if (checkInDate > DateTime.UtcNow)
                throw new ArgumentException("Check-in date cannot be in the future.", nameof(checkInDate));

            if (!string.IsNullOrWhiteSpace(ageRange) && !AgeRangeHelper.IsValidAgeRange(ageRange))
                throw new ArgumentException("Age range must be one of: 'Under 18', '18-24', '25-34', '35-44', '45-54', '55+'.", nameof(ageRange));

            if (!FocusAreasHelper.IsValidFocusAreasList(focusAreas))
                throw new ArgumentException($"Focus areas must be valid and cannot exceed {FocusAreasHelper.MaxFocusAreas} selections.", nameof(focusAreas));

            if (!string.IsNullOrWhiteSpace(stressNotes) && stressNotes.Length > 500)
                throw new ArgumentException("Stress notes cannot exceed 500 characters.", nameof(stressNotes));

            if (!string.IsNullOrWhiteSpace(thoughtTrackingMethod) && !ThoughtTrackingHelper.IsValidThoughtTrackingMethod(thoughtTrackingMethod))
                throw new ArgumentException("Thought tracking method must be one of: 'Notes app or planner', 'Paper journal', 'In my head', 'With an app'.", nameof(thoughtTrackingMethod));

            if (!SupportAreasHelper.IsValidSupportAreasList(supportAreas))
                throw new ArgumentException($"Support areas must be valid and cannot exceed {SupportAreasHelper.MaxSupportAreas} selections.", nameof(supportAreas));

            if (!string.IsNullOrWhiteSpace(selfCareFrequency) && !SelfCareFrequencyHelper.IsValidSelfCareFrequency(selfCareFrequency))
                throw new ArgumentException("Self-care frequency must be one of: 'Rarely', 'Sometimes', 'Often', 'Daily'.", nameof(selfCareFrequency));

            if (!string.IsNullOrWhiteSpace(toughDayMessage) && toughDayMessage.Length > 500)
                throw new ArgumentException("Tough day message cannot exceed 500 characters.", nameof(toughDayMessage));

            if (!CopingMechanismsHelper.IsValidCopingMechanismsList(copingMechanisms))
                throw new ArgumentException($"Coping mechanisms must be valid and cannot exceed {CopingMechanismsHelper.MaxCopingMechanisms} selections.", nameof(copingMechanisms));

            if (!string.IsNullOrWhiteSpace(joyPeaceSources) && joyPeaceSources.Length > 500)
                throw new ArgumentException("Joy/peace sources cannot exceed 500 characters.", nameof(joyPeaceSources));

            // Validate time fields
            ValidateTimeField(weekdayStartTime, weekdayStartShift, weekdayEndTime, weekdayEndShift, "Weekday");
            ValidateTimeField(weekendStartTime, weekendStartShift, weekendEndTime, weekendEndShift, "Weekend");
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
using System;
using Mindflow_Web_API.Utilities;

namespace Mindflow_Web_API.Models
{
    public class WellnessCheckIn : EntityBase
    {
        public Guid UserId { get; set; }
        public string MoodLevel { get; set; }            // 'neutral', 'happy', 'sad'
        public DateTime CheckInDate { get; set; }
        public string? WeekdayFreeTime { get; set; }
        public string? WeekendFreeTime { get; set; }
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

        // Private constructor for ORM frameworks
        private WellnessCheckIn()
        {
            MoodLevel = string.Empty;
        }

        private WellnessCheckIn(Guid userId, string mood, DateTime checkInDate, string? weekdayFreeTime, string? weekendFreeTime, bool reminderEnabled, string? reminderTime, string? ageRange, string[]? focusAreas, string? stressNotes, string? thoughtTrackingMethod, string[]? supportAreas, string? selfCareFrequency, string? toughDayMessage, string[]? copingMechanisms, string? joyPeaceSources)
        {
            UserId = userId;
            MoodLevel = mood;
            CheckInDate = checkInDate;
            WeekdayFreeTime = weekdayFreeTime;
            WeekendFreeTime = weekendFreeTime;
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
        }

        public static WellnessCheckIn Create(Guid userId, string mood, DateTime checkInDate, string? weekdayFreeTime = null, string? weekendFreeTime = null, bool reminderEnabled = false, string? reminderTime = null, string? ageRange = null, string[]? focusAreas = null, string? stressNotes = null, string? thoughtTrackingMethod = null, string[]? supportAreas = null, string? selfCareFrequency = null, string? toughDayMessage = null, string[]? copingMechanisms = null, string? joyPeaceSources = null)
        {
            ValidateInputs(mood, checkInDate, ageRange, focusAreas, stressNotes, thoughtTrackingMethod, supportAreas, selfCareFrequency, toughDayMessage, copingMechanisms, joyPeaceSources);
            return new WellnessCheckIn(userId, mood, checkInDate, weekdayFreeTime, weekendFreeTime, reminderEnabled, reminderTime, ageRange, focusAreas, stressNotes, thoughtTrackingMethod, supportAreas, selfCareFrequency, toughDayMessage, copingMechanisms, joyPeaceSources);
        }

        public void Update(string mood, DateTime checkInDate, string? weekdayFreeTime, string? weekendFreeTime, bool reminderEnabled, string? reminderTime, string? ageRange, string[]? focusAreas, string? stressNotes, string? thoughtTrackingMethod, string[]? supportAreas, string? selfCareFrequency, string? toughDayMessage, string[]? copingMechanisms, string? joyPeaceSources)
        {
            ValidateInputs(mood, checkInDate, ageRange, focusAreas, stressNotes, thoughtTrackingMethod, supportAreas, selfCareFrequency, toughDayMessage, copingMechanisms, joyPeaceSources);

            MoodLevel = mood;
            CheckInDate = checkInDate;
            WeekdayFreeTime = weekdayFreeTime;
            WeekendFreeTime = weekendFreeTime;
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

            UpdateLastModified();
        }

        private static void ValidateInputs(string mood, DateTime checkInDate, string? ageRange, string[]? focusAreas, string? stressNotes, string? thoughtTrackingMethod, string[]? supportAreas, string? selfCareFrequency, string? toughDayMessage, string[]? copingMechanisms, string? joyPeaceSources)
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
        }
    }
} 
using System;

namespace Mindflow_Web_API.Models
{
    public class WellnessCheckIn : EntityBase
    {
        public Guid UserId { get; set; }
        public int StressLevel { get; set; }          // e.g., 1–10
        public string MoodLevel { get; set; }            // 'neutral', 'happy', 'sad'
        public string EnergyLevel { get; set; }       // 1:Low, 2:Medium, 3:High
        public int SpiritualWellness { get; set; }    // 1–10
        public DateTime CheckInDate { get; set; }
        public string? WeekdayFreeTime { get; set; }
        public string? WeekendFreeTime { get; set; }
        public bool ReminderEnabled { get; set; }
        public string? ReminderTime { get; set; }

        // Private constructor for ORM frameworks
        private WellnessCheckIn()
        {
            EnergyLevel = string.Empty;
        }

        private WellnessCheckIn(Guid userId, int stress, string mood, string energy, int spiritual, DateTime checkInDate, string? weekdayFreeTime, string? weekendFreeTime, bool reminderEnabled, string? reminderTime)
        {
            UserId = userId;
            StressLevel = stress;
            MoodLevel = mood;
            EnergyLevel = energy;
            SpiritualWellness = spiritual;
            CheckInDate = checkInDate;
            WeekdayFreeTime = weekdayFreeTime;
            WeekendFreeTime = weekendFreeTime;
            ReminderEnabled = reminderEnabled;
            ReminderTime = reminderTime;
        }

        public static WellnessCheckIn Create(Guid userId, int stress, string mood, string energy, int spiritual, DateTime checkInDate, string? weekdayFreeTime = null, string? weekendFreeTime = null, bool reminderEnabled = false, string? reminderTime = null)
        {
            ValidateInputs(stress, mood, energy, spiritual, checkInDate);
            return new WellnessCheckIn(userId, stress, mood, energy, spiritual, checkInDate, weekdayFreeTime, weekendFreeTime, reminderEnabled, reminderTime);
        }

        public void Update(int stress, string mood, string energy, int spiritual, DateTime checkInDate, string? weekdayFreeTime, string? weekendFreeTime, bool reminderEnabled, string? reminderTime)
        {
            ValidateInputs(stress, mood, energy, spiritual, checkInDate);

            StressLevel = stress;
            MoodLevel = mood;
            EnergyLevel = energy;
            SpiritualWellness = spiritual;
            CheckInDate = checkInDate;
            WeekdayFreeTime = weekdayFreeTime;
            WeekendFreeTime = weekendFreeTime;
            ReminderEnabled = reminderEnabled;
            ReminderTime = reminderTime;

            UpdateLastModified();
        }

        private static void ValidateInputs(int stress, string mood, string energy, int spiritual, DateTime checkInDate)
        {
            if (stress < 1 || stress > 10)
                throw new ArgumentException("Stress level must be between 1 and 10.", nameof(stress));

            if (string.IsNullOrWhiteSpace(mood) || (mood != "neutral" && mood != "happy" && mood != "sad"))
                throw new ArgumentException("Mood level must be one of: 'neutral', 'happy', 'sad'.", nameof(mood));

            if (string.IsNullOrWhiteSpace(energy))
                throw new ArgumentException("Energy level cannot be null or empty.", nameof(energy));

            if (spiritual < 1 || spiritual > 10)
                throw new ArgumentException("Spiritual wellness must be between 1 and 10.", nameof(spiritual));

            if (checkInDate > DateTime.UtcNow)
                throw new ArgumentException("Check-in date cannot be in the future.", nameof(checkInDate));
        }
    }
} 
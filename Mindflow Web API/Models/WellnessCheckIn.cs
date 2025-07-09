using System;

namespace Mindflow_Web_API.Models
{
    public class WellnessCheckIn : EntityBase
    {
        public Guid UserId { get; set; }
        public int StressLevel { get; set; }          // e.g., 1–10
        public int MoodLevel { get; set; }            // e.g., 1:Sad, 2:Neutral, 3:Happy
        public string EnergyLevel { get; set; }       // 1:Low, 2:Medium, 3:High
        public int SpiritualWellness { get; set; }    // 1–10
        public DateTime CheckInDate { get; set; }

        // Private constructor for ORM frameworks
        private WellnessCheckIn()
        {
            EnergyLevel = string.Empty;
        }

        private WellnessCheckIn(Guid userId, int stress, int mood, string energy, int spiritual, DateTime checkInDate)
        {
            UserId = userId;
            StressLevel = stress;
            MoodLevel = mood;
            EnergyLevel = energy;
            SpiritualWellness = spiritual;
            CheckInDate = checkInDate;
        }

        public static WellnessCheckIn Create(Guid userId, int stress, int mood, string energy, int spiritual, DateTime checkInDate)
        {
            ValidateInputs(stress, mood, energy, spiritual, checkInDate);
            return new WellnessCheckIn(userId, stress, mood, energy, spiritual, checkInDate);
        }

        public void Update(int stress, int mood, string energy, int spiritual, DateTime checkInDate)
        {
            ValidateInputs(stress, mood, energy, spiritual, checkInDate);

            StressLevel = stress;
            MoodLevel = mood;
            EnergyLevel = energy;
            SpiritualWellness = spiritual;
            CheckInDate = checkInDate;

            UpdateLastModified();
        }

        private static void ValidateInputs(int stress, int mood, string energy, int spiritual, DateTime checkInDate)
        {
            if (stress < 1 || stress > 10)
                throw new ArgumentException("Stress level must be between 1 and 10.", nameof(stress));

            if (mood < 1 || mood > 3)
                throw new ArgumentException("Mood level must be between 1 and 3.", nameof(mood));

            if (string.IsNullOrWhiteSpace(energy))
                throw new ArgumentException("Energy level cannot be null or empty.", nameof(energy));

            if (spiritual < 1 || spiritual > 10)
                throw new ArgumentException("Spiritual wellness must be between 1 and 10.", nameof(spiritual));

            if (checkInDate > DateTime.UtcNow)
                throw new ArgumentException("Check-in date cannot be in the future.", nameof(checkInDate));
        }
    }
} 
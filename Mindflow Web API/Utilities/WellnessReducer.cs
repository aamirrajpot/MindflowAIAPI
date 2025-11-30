using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Utilities
{
    public static class WellnessReducer
    {
        public static WellnessSummary Reduce(WellnessCheckInDto? w)
        {
            var summary = new WellnessSummary();

            if (w == null)
                return summary;

            // 1. MoodLevel
            summary.MoodLevel = w.MoodLevel;

            // 2. Focus Areas as-is
            if (w.FocusAreas != null)
            {
                summary.FocusAreas = w.FocusAreas
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .ToList();
            }

            // 3. Convert Start/End time into morning/afternoon/evening buckets
            summary.PreferredTimeBlocks = ExtractTimeBlocks(w);

            // 4. Extract meaningful numeric answers or short responses from Questions
            if (w.Questions != null)
            {
                foreach (var kv in w.Questions)
                {
                    if (kv.Value == null) continue;

                    // Keep only short and relevant values
                    if (kv.Value is int or double or float or bool or string)
                    {
                        summary.KeyResponses[kv.Key] = kv.Value;
                    }
                }
            }

            return summary;
        }

        private static List<string> ExtractTimeBlocks(WellnessCheckInDto w)
        {
            var blocks = new List<string>();

            // Convert weekday/weekend to simple labels
            // You may refine this logic further as needed.

            if (IsMorning(w.WeekdayStartTime) || IsMorning(w.WeekendStartTime))
                blocks.Add("morning");

            if (IsAfternoon(w.WeekdayStartTime) || IsAfternoon(w.WeekendStartTime))
                blocks.Add("afternoon");

            if (IsEvening(w.WeekdayEndTime) || IsEvening(w.WeekendEndTime))
                blocks.Add("evening");

            return blocks.Distinct().ToList();
        }

        private static bool IsMorning(string? time)
            => TimeMatches(time, 5, 11);

        private static bool IsAfternoon(string? time)
            => TimeMatches(time, 12, 17);

        private static bool IsEvening(string? time)
            => TimeMatches(time, 18, 23);

        private static bool TimeMatches(string? timeStr, int startHour, int endHour)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return false;

            // always try HH:mm format first
            if (TimeSpan.TryParse(timeStr, out var t))
            {
                return t.Hours >= startHour && t.Hours <= endHour;
            }

            return false;
        }
    }
}

using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.DTOs
{
    // Request DTOs
    public record WellnessAnalysisRequest(
        Guid UserId,
        WellnessCheckInDto WellnessData
    );

    // Response DTOs
    public record WellnessAnalysisResponse(
        Guid Id,
        Guid UserId,
        WellnessCheckInDto WellnessData,
        WellnessAnalysisDto Analysis,
        DateTime GeneratedAt
    );

    public record WellnessAnalysisDto(
        string MoodAssessment,
        string StressLevel,
        List<string> SupportNeeds,
        List<string> CopingStrategies,
        List<string> SelfCareSuggestions,
        string ProgressTracking,
        int UrgencyLevel,
        List<string> ImmediateActions,
        List<string> LongTermGoals
    );

    // Summary DTO for the "You're all set!" screen
    public record WellnessSummaryDto(
        string PrimaryFocus, // e.g., "spirituality"
        string SelfCareFrequency, // e.g., "daily"
        int SupportNeedsCount, // e.g., "2 selected"
        List<string> TopSupportNeeds,
        List<string> RecommendedActions,
        int UrgencyLevel,
        string PersonalizedMessage,
        // New fields for meaningful insights
        List<string>? Insights = null, // e.g., "Your stress mentions dropped 20% this week"
        List<string>? Patterns = null, // e.g., "You've mentioned exhaustion 3 times this week"
        ProgressMetricsDto? ProgressMetrics = null, // Task completion, brain dump frequency, etc.
        EmotionTrendsDto? EmotionTrends = null // Emotion keyword tracking
    );

    public record ProgressMetricsDto(
        double TaskCompletionRate, // Percentage of completed tasks
        int BrainDumpFrequency, // Number of brain dumps this week
        int BrainDumpFrequencyChange, // Change from last week (+/-)
        double AverageMoodScore, // Average mood score this week
        double AverageMoodScoreChange, // Change from last week
        double AverageStressScore, // Average stress score this week
        double AverageStressScoreChange, // Change from last week
        string Interpretation // e.g., "Your mood scores have improved from 5/10 to 7/10"
    );

    public record EmotionTrendsDto(
        Dictionary<string, int> EmotionFrequency, // e.g., { "anxious": 5, "grateful": 3 }
        List<string> TopEmotions, // Top 3 emotions this week
        List<string> EmotionInsights // e.g., "You've mentioned 'exhaustion' 3 times this week"
    );

    // Wellness Snapshot DTOs for chart data
    public record WellnessSnapshotDto(
        List<WellnessDataPointDto> DataPoints,
        WellnessTrendsDto Trends,
        WellnessInsightsDto Insights
    );

    public record WellnessDataPointDto(
        DateTime Date,
        string DayOfWeek,
        double? Mood,
        double? Energy,
        double? Stress,
        int EntryCount
    );

    public record WellnessTrendsDto(
        string MoodTrend, // "improving", "declining", "stable"
        string EnergyTrend,
        string StressTrend,
        double MoodChangePercentage,
        double EnergyChangePercentage,
        double StressChangePercentage
    );

    public record WellnessInsightsDto(
        List<string> MoodInsights,
        List<string> EnergyInsights,
        List<string> StressInsights,
        List<string> Recommendations
    );

    // Analytics DTO for separate analytics endpoint
    public record AnalyticsDto(
        List<string>? Insights, // e.g., "Your stress mentions dropped 20% this week"
        List<string>? Patterns, // e.g., "You've mentioned exhaustion 3 times this week"
        ProgressMetricsDto? ProgressMetrics, // Task completion, brain dump frequency, etc.
        EmotionTrendsDto? EmotionTrends, // Emotion keyword tracking
        string? PersonalizedMessage // Enhanced personalized message with insights
    );
}

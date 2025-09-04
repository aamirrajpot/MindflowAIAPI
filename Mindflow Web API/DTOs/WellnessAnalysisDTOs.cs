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
        string PersonalizedMessage
    );
}

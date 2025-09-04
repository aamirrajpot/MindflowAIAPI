using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.DTOs
{
    // Request DTOs
    public record CreateJournalEntryDto(
        string Text,
        string? Title = null,
        string? Context = null,
        int? Mood = null,
        int? Stress = null,
        int? Purpose = null,
        string? Tags = null,
        bool IsFavorite = false
    );

    public record UpdateJournalEntryDto(
        string? Text = null,
        string? Title = null,
        string? Context = null,
        int? Mood = null,
        int? Stress = null,
        int? Purpose = null,
        string? Tags = null,
        bool? IsFavorite = null
    );

    public record JournalSearchDto(
        string? Query = null,
        string? Filter = null, // "all", "recent", "favorites"
        int Page = 1,
        int PageSize = 10
    );

    // Response DTOs
    public record JournalEntryDto(
        Guid Id,
        Guid UserId,
        string Text,
        string? Title,
        string? Context,
        int? Mood,
        int? Stress,
        int? Purpose,
        DateTime CreatedAtUtc,
        BrainDumpSource Source,
        string? SuggestionsPreview,
        bool IsFlagged,
        string? FlagReason,
        DateTime? DeletedAtUtc,
        // Journal-specific fields
        string? Tags,
        bool IsFavorite,
        string? AiInsight,
        int WordCount
    );

    public record JournalStatsDto(
        int TotalEntries,
        int CurrentStreak,
        int TotalWords,
        int ThisMonthEntries,
        int ThisWeekEntries
    );

    public record JournalListResponseDto(
        List<JournalEntryDto> Entries,
        int TotalCount,
        int Page,
        int PageSize,
        bool HasMore
    );

    public record AiInsightDto(
        string Insight,
        string Type, // "pattern", "emotion", "frequency", etc.
        DateTime GeneratedAt
    );
}

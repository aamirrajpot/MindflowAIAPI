using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IJournalService
    {
        // CRUD operations
        Task<JournalEntryDto> CreateEntryAsync(Guid userId, CreateJournalEntryDto dto);
        Task<JournalEntryDto?> GetEntryByIdAsync(Guid userId, Guid entryId);
        Task<JournalEntryDto?> UpdateEntryAsync(Guid userId, Guid entryId, UpdateJournalEntryDto dto);
        Task<bool> DeleteEntryAsync(Guid userId, Guid entryId);
        
        // List and search operations
        Task<JournalListResponseDto> GetEntriesAsync(Guid userId, JournalSearchDto searchDto);
        Task<List<JournalEntryDto>> GetRecentEntriesAsync(Guid userId, int count = 5);
        
        // Statistics
        Task<JournalStatsDto> GetStatsAsync(Guid userId);
        
        // AI insights
        Task<string?> GenerateAiInsightAsync(Guid userId, Guid entryId);
        Task<List<AiInsightDto>> GetAiInsightsAsync(Guid userId, int count = 10);
        
        // Utility methods
        Task<int> CalculateWordCountAsync(string text);
        Task<List<string>> ExtractTagsAsync(string text);
    }
}

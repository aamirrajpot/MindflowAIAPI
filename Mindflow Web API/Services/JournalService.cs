using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System.Text.RegularExpressions;

namespace Mindflow_Web_API.Services
{
    public class JournalService : IJournalService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<JournalService> _logger;
        private readonly IRunPodService _runPodService;

        public JournalService(MindflowDbContext dbContext, ILogger<JournalService> logger, IRunPodService runPodService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _runPodService = runPodService;
        }

        public async Task<JournalEntryDto> CreateEntryAsync(Guid userId, CreateJournalEntryDto dto)
        {
            var wordCount = await CalculateWordCountAsync(dto.Text);
            var extractedTags = await ExtractTagsAsync(dto.Text);
            var tagsString = string.Join(",", extractedTags);

            var entry = new BrainDumpEntry
            {
                UserId = userId,
                Text = dto.Text,
                Title = dto.Title ?? GenerateDefaultTitle(dto.Text),
                Context = dto.Context,
                Mood = dto.Mood,
                Stress = dto.Stress,
                Purpose = dto.Purpose,
                CreatedAtUtc = DateTime.UtcNow,
                Source = BrainDumpSource.Mobile, // Default to mobile for journal entries
                WordCount = wordCount,
                Tags = !string.IsNullOrEmpty(dto.Tags) ? dto.Tags : tagsString,
                IsFavorite = dto.IsFavorite,
                TokensEstimate = dto.Text?.Length
            };

            _dbContext.BrainDumpEntries.Add(entry);
            await _dbContext.SaveChangesAsync();

            // Generate AI insight asynchronously (don't await to avoid blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var insight = await GenerateAiInsightAsync(userId, entry.Id);
                    if (!string.IsNullOrEmpty(insight))
                    {
                        entry.AiInsight = insight;
                        await _dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate AI insight for entry {EntryId}", entry.Id);
                }
            });

            return ToDto(entry);
        }

        public async Task<JournalEntryDto?> GetEntryByIdAsync(Guid userId, Guid entryId)
        {
            var entry = await _dbContext.BrainDumpEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId && e.DeletedAtUtc == null);
            
            return entry == null ? null : ToDto(entry);
        }

        public async Task<JournalEntryDto?> UpdateEntryAsync(Guid userId, Guid entryId, UpdateJournalEntryDto dto)
        {
            var entry = await _dbContext.BrainDumpEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId && e.DeletedAtUtc == null);
            
            if (entry == null) return null;

            if (dto.Text != null)
            {
                entry.Text = dto.Text;
                entry.WordCount = await CalculateWordCountAsync(dto.Text);
                entry.TokensEstimate = dto.Text.Length;
            }
            
            if (dto.Title != null) entry.Title = dto.Title;
            if (dto.Context != null) entry.Context = dto.Context;
            if (dto.Mood.HasValue) entry.Mood = dto.Mood.Value;
            if (dto.Stress.HasValue) entry.Stress = dto.Stress.Value;
            if (dto.Purpose.HasValue) entry.Purpose = dto.Purpose.Value;
            if (dto.Tags != null) entry.Tags = dto.Tags;
            if (dto.IsFavorite.HasValue) entry.IsFavorite = dto.IsFavorite.Value;

            await _dbContext.SaveChangesAsync();
            return ToDto(entry);
        }

        public async Task<bool> DeleteEntryAsync(Guid userId, Guid entryId)
        {
            var entry = await _dbContext.BrainDumpEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId && e.DeletedAtUtc == null);
            
            if (entry == null) return false;

            entry.DeletedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<JournalListResponseDto> GetEntriesAsync(Guid userId, JournalSearchDto searchDto)
        {
            var query = _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null);

            // Apply search filter
            if (!string.IsNullOrEmpty(searchDto.Query))
            {
                query = query.Where(e => e.Text.Contains(searchDto.Query) || 
                                       (e.Title != null && e.Title.Contains(searchDto.Query)));
            }

            // Apply filter type
            switch (searchDto.Filter?.ToLower())
            {
                case "recent":
                    var recentDate = DateTime.UtcNow.AddDays(-7);
                    query = query.Where(e => e.CreatedAtUtc >= recentDate);
                    break;
                case "favorites":
                    query = query.Where(e => e.IsFavorite);
                    break;
                // "all" or null - no additional filter
            }

            var totalCount = await query.CountAsync();

            var entries = await query
                .OrderByDescending(e => e.CreatedAtUtc)
                .Skip((searchDto.Page - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .Select(e => ToDto(e))
                .ToListAsync();

            return new JournalListResponseDto(
                entries,
                totalCount,
                searchDto.Page,
                searchDto.PageSize,
                (searchDto.Page * searchDto.PageSize) < totalCount
            );
        }

        public async Task<List<JournalEntryDto>> GetRecentEntriesAsync(Guid userId, int count = 5)
        {
            var entries = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(count)
                .Select(e => ToDto(e))
                .ToListAsync();

            return entries;
        }

        public async Task<JournalStatsDto> GetStatsAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek);

            var totalEntries = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null)
                .CountAsync();

            var totalWords = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null)
                .SumAsync(e => e.WordCount);

            var thisMonthEntries = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null && e.CreatedAtUtc >= startOfMonth)
                .CountAsync();

            var thisWeekEntries = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null && e.CreatedAtUtc >= startOfWeek)
                .CountAsync();

            var currentStreak = await CalculateStreakAsync(userId);

            return new JournalStatsDto(
                totalEntries,
                currentStreak,
                totalWords,
                thisMonthEntries,
                thisWeekEntries
            );
        }

        public async Task<string?> GenerateAiInsightAsync(Guid userId, Guid entryId)
        {
            try
            {
                var entry = await _dbContext.BrainDumpEntries
                    .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId);
                
                if (entry == null) return null;

                // Get recent entries for context
                var recentEntries = await _dbContext.BrainDumpEntries
                    .Where(e => e.UserId == userId && e.DeletedAtUtc == null && e.CreatedAtUtc >= DateTime.UtcNow.AddDays(-30))
                    .OrderByDescending(e => e.CreatedAtUtc)
                    .Take(10)
                    .ToListAsync();

                var prompt = BuildInsightPrompt(entry, recentEntries);
                var response = await _runPodService.SendPromptAsync(prompt, 500, 0.7);
                
                // Parse the response to extract insight
                var insight = ParseInsightResponse(response);
                return insight;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate AI insight for entry {EntryId}", entryId);
                return null;
            }
        }

        public async Task<List<AiInsightDto>> GetAiInsightsAsync(Guid userId, int count = 10)
        {
            var entries = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null && !string.IsNullOrEmpty(e.AiInsight))
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(count)
                .Select(e => new AiInsightDto(e.AiInsight!, "pattern", e.CreatedAtUtc))
                .ToListAsync();

            return entries;
        }

        public async Task<int> CalculateWordCountAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            
            // Simple word count - split by whitespace and count non-empty parts
            var words = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length;
        }

        public async Task<List<string>> ExtractTagsAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            // Simple tag extraction - look for common emotional/wellness keywords
            var commonTags = new[] { "gratitude", "anxiety", "stress", "happy", "sad", "tired", "energized", 
                                   "overwhelmed", "peaceful", "frustrated", "excited", "worried", "calm", 
                                   "morning", "evening", "work", "family", "health", "exercise", "meditation" };

            var textLower = text.ToLower();
            var foundTags = commonTags.Where(tag => textLower.Contains(tag)).ToList();

            return foundTags;
        }

        // Private helper methods
        private static JournalEntryDto ToDto(BrainDumpEntry entry)
        {
            return new JournalEntryDto(
                entry.Id,
                entry.UserId,
                entry.Text,
                entry.Title,
                entry.Context,
                entry.Mood,
                entry.Stress,
                entry.Purpose,
                entry.CreatedAtUtc,
                entry.Source,
                entry.SuggestionsPreview,
                entry.IsFlagged,
                entry.FlagReason,
                entry.DeletedAtUtc,
                entry.Tags,
                entry.IsFavorite,
                entry.AiInsight,
                entry.WordCount
            );
        }

        private static string GenerateDefaultTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Untitled Entry";
            
            // Take first 50 characters and clean up
            var title = text.Substring(0, Math.Min(50, text.Length)).Trim();
            if (title.Length < text.Length) title += "...";
            
            return title;
        }

        private async Task<int> CalculateStreakAsync(Guid userId)
        {
            var entries = await _dbContext.BrainDumpEntries
                .Where(e => e.UserId == userId && e.DeletedAtUtc == null)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Select(e => e.CreatedAtUtc.Date)
                .Distinct()
                .ToListAsync();

            if (!entries.Any()) return 0;

            var currentDate = DateTime.UtcNow.Date;
            var streak = 0;

            foreach (var entryDate in entries)
            {
                if (entryDate == currentDate || entryDate == currentDate.AddDays(-streak))
                {
                    streak++;
                    currentDate = entryDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private static string BuildInsightPrompt(BrainDumpEntry entry, List<BrainDumpEntry> recentEntries)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[INST] Analyze this journal entry and provide a brief, helpful insight. ");
            sb.Append("Consider patterns, emotions, or themes. Keep it under 100 characters.\n\n");
            sb.Append("Current Entry:\n");
            sb.Append(entry.Text);
            sb.Append("\n\n");
            
            if (recentEntries.Any())
            {
                sb.Append("Recent entries for context:\n");
                foreach (var recent in recentEntries.Take(5))
                {
                    sb.Append($"- {recent.CreatedAtUtc:MMM dd}: {recent.Text.Substring(0, Math.Min(100, recent.Text.Length))}...\n");
                }
            }
            
            sb.Append("\nProvide a brief insight like: 'You journal about 'overwhelm' most on Mondays' or 'You use the word 'tired' frequently when stressed'.\n");
            sb.Append("Return only the insight text, no additional formatting. [/INST]");
            
            return sb.ToString();
        }

        private static string ParseInsightResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return string.Empty;
            
            // Clean up the response - remove any JSON formatting or extra text
            var cleanResponse = response.Trim();
            
            // If it's wrapped in quotes, remove them
            if (cleanResponse.StartsWith("\"") && cleanResponse.EndsWith("\""))
            {
                cleanResponse = cleanResponse.Substring(1, cleanResponse.Length - 2);
            }
            
            return cleanResponse;
        }
    }
}

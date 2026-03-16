using System;

namespace Mindflow_Web_API.Models
{
    /// <summary>
    /// Minimal record of a user reporting an AI-generated response
    /// (summary, suggestion, or other) as inappropriate or unhelpful.
    /// </summary>
    public class AiContentReport : EntityBase
    {
        public Guid UserId { get; set; }

        public Guid? BrainDumpEntryId { get; set; }

        /// <summary>
        /// Optional identifier of the specific suggestion or content block.
        /// </summary>
        public Guid? SuggestionId { get; set; }

        /// <summary>
        /// High-level type, e.g. \"summary\", \"taskSuggestion\", \"other\".
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Optional free-text reason or user comment.
        /// </summary>
        public string? Reason { get; set; }
    }
}


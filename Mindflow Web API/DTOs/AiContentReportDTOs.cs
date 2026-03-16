using System;
using System.ComponentModel.DataAnnotations;

namespace Mindflow_Web_API.DTOs
{
    public class CreateAiContentReportDto
    {
        [Required]
        public Guid BrainDumpEntryId { get; set; }

        /// <summary>
        /// Optional: ID of a specific suggestion that was problematic.
        /// </summary>
        public Guid? SuggestionId { get; set; }

        /// <summary>
        /// \"summary\", \"taskSuggestion\", or \"other\".
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Optional free-text reason from the user.
        /// </summary>
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }
}


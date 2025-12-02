using System.ComponentModel.DataAnnotations;

namespace Mindflow_Web_API.DTOs
{
    public class TextPredictionRequest
    {
        [Required]
        public string Prompt { get; set; } = string.Empty;
    }
}



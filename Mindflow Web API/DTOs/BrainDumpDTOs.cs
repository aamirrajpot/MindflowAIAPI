using System;
using System.ComponentModel.DataAnnotations;

namespace Mindflow_Web_API.DTOs
{
	public class BrainDumpRequest
	{
		[Required]
		[MinLength(3)]
		public string Text { get; set; } = string.Empty;

		public string? Context { get; set; }

		// Optional sliders from UI (0-10). Nullable to keep payload light
		[Range(0, 10)]
		public int? Mood { get; set; }

		[Range(0, 10)]
		public int? Stress { get; set; }

		[Range(0, 10)]
		public int? Purpose { get; set; }
	}
}



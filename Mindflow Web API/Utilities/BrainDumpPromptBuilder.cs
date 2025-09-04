using Mindflow_Web_API.DTOs;
using System.Text;

namespace Mindflow_Web_API.Utilities
{
	public static class BrainDumpPromptBuilder
	{
		public static string BuildTaskSuggestionsPrompt(BrainDumpRequest request)
		{
			var sb = new StringBuilder();
			sb.Append("[INST] You are a helpful assistant that converts a brain dump into practical, supportive tasks. ");
			sb.Append("Return 3-5 tasks tailored to the user's emotional state and context. ");
			sb.Append("Each task must be concise, actionable, and supportive.\n\n");

			sb.Append("User Brain Dump:\n");
			sb.Append(request.Text.Replace("\r", "").Replace("\n", "\\n"));
			sb.Append("\n\n");

			if (!string.IsNullOrWhiteSpace(request.Context))
			{
				sb.Append("Additional Context:\n");
				sb.Append(request.Context!.Replace("\r", "").Replace("\n", "\\n"));
				sb.Append("\n\n");
			}

			if (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue)
			{
				sb.Append("Well-being Scores (0-10): ");
				if (request.Mood.HasValue) sb.Append($"Mood={request.Mood.Value} ");
				if (request.Stress.HasValue) sb.Append($"Stress={request.Stress.Value} ");
				if (request.Purpose.HasValue) sb.Append($"Purpose={request.Purpose.Value} ");
				sb.Append("\n\n");
			}

			sb.Append("Return your answer strictly as JSON array of objects with keys: task, frequency, duration, notes.\n");
			sb.Append("Example: [ { \"task\": \"Journaling\", \"frequency\": \"Daily\", \"duration\": \"10-15 minutes\", \"notes\": \"Keep it light\" } ] ");
			sb.Append("Do not include numbering or extra commentary outside JSON. [/INST]");

			return sb.ToString();
		}
	}
}



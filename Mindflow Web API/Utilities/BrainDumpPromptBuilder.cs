using Mindflow_Web_API.DTOs;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using JsonRepairSharp;

namespace Mindflow_Web_API.Utilities
{
	public static class BrainDumpPromptBuilder
	{
		public static string BuildTaskSuggestionsPrompt(BrainDumpRequest request, DTOs.WellnessCheckInDto? wellnessData = null, string? userName = null)
		{
			var sb = new StringBuilder();
			sb.Append("[INST] You are a helpful assistant that analyzes brain dumps and provides comprehensive insights. ");
			sb.Append("You must return a JSON object with the following structure:\n\n");
			sb.Append("{\n");
			sb.Append("  \"userProfile\": {\n");
			sb.Append($"    \"name\": \"{userName ?? "User"}\",\n");
			sb.Append("    \"currentState\": \"Brief emotional state description (e.g., 'Reflective & Optimistic')\",\n");
			sb.Append("    \"emoji\": \"Relevant emoji (e.g., '😊', '🤔', '💪')\"\n");
			sb.Append("  },\n");
			sb.Append("  \"keyThemes\": [\"Theme 1\", \"Theme 2\", \"Theme 3\"],\n");
			sb.Append("  \"aiSummary\": \"Comprehensive 2-3 sentence summary of the user's emotional state and mindset\",\n");
			sb.Append("  \"suggestedActivities\": [\n");
			sb.Append("    {\n");
			sb.Append("      \"task\": \"Activity name\",\n");
			sb.Append("      \"frequency\": \"How often\",\n");
			sb.Append("      \"duration\": \"Time needed\",\n");
			sb.Append("      \"notes\": \"Why this helps and how to do it\"\n");
			sb.Append("    }\n");
			sb.Append("  ]\n");
			sb.Append("}\n\n");

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

			// Add wellness data context if available
			if (wellnessData != null)
			{
				sb.Append("User's Wellness Profile:\n");
				
				// Available time slots
				if (!string.IsNullOrWhiteSpace(wellnessData.WeekdayStartTime) || !string.IsNullOrWhiteSpace(wellnessData.WeekendStartTime))
				{
					sb.Append("Available Time Slots:\n");
					if (!string.IsNullOrWhiteSpace(wellnessData.WeekdayStartTime))
					{
						sb.Append($"- Weekdays: {wellnessData.WeekdayStartTime} {wellnessData.WeekdayStartShift} to {wellnessData.WeekdayEndTime} {wellnessData.WeekdayEndShift}\n");
					}
					if (!string.IsNullOrWhiteSpace(wellnessData.WeekendStartTime))
					{
						sb.Append($"- Weekends: {wellnessData.WeekendStartTime} {wellnessData.WeekendStartShift} to {wellnessData.WeekendEndTime} {wellnessData.WeekendEndShift}\n");
					}
					sb.Append("\n");
				}

				// Focus areas
				if (wellnessData.FocusAreas != null && wellnessData.FocusAreas.Length > 0)
				{
					sb.Append($"Focus Areas: {string.Join(", ", wellnessData.FocusAreas)}\n");
				}

				// Support needs
				if (wellnessData.SupportAreas != null && wellnessData.SupportAreas.Length > 0)
				{
					sb.Append($"Support Needs: {string.Join(", ", wellnessData.SupportAreas)}\n");
				}

				// Coping mechanisms
				if (wellnessData.CopingMechanisms != null && wellnessData.CopingMechanisms.Length > 0)
				{
					sb.Append($"Preferred Coping Methods: {string.Join(", ", wellnessData.CopingMechanisms)}\n");
				}

				// Self-care frequency
				if (!string.IsNullOrWhiteSpace(wellnessData.SelfCareFrequency))
				{
					sb.Append($"Current Self-Care Frequency: {wellnessData.SelfCareFrequency}\n");
				}

				// Joy/peace sources
				if (!string.IsNullOrWhiteSpace(wellnessData.JoyPeaceSources))
				{
					sb.Append($"Sources of Joy/Peace: {wellnessData.JoyPeaceSources}\n");
				}

				// Age range for context
				if (!string.IsNullOrWhiteSpace(wellnessData.AgeRange))
				{
					sb.Append($"Age Range: {wellnessData.AgeRange}\n");
				}

				sb.Append("\n");
			}

			sb.Append("Analysis Guidelines:\n");
			sb.Append("- User Profile: Create a brief, positive emotional state description (2-3 words + adjective)\n");
			sb.Append("- Key Themes: Extract 3 main topics/concerns from the brain dump (e.g., 'Personal Growth', 'Work Balance', 'Mindfulness')\n");
			sb.Append("- AI Summary: Write a supportive, empathetic 2-3 sentence summary focusing on positive aspects and growth\n");
			sb.Append("- Suggested Activities: 3-5 practical, actionable tasks that:\n");
			sb.Append("  * Consider the user's available time slots when suggesting duration\n");
			sb.Append("  * Align with their focus areas and support needs\n");
			sb.Append("  * Incorporate their preferred coping mechanisms when relevant\n");
			sb.Append("  * Respect their current self-care frequency level\n");
			sb.Append("  * Build on their existing sources of joy and peace\n");
			sb.Append("  * Are realistic and achievable within their schedule\n\n");

			sb.Append("Return ONLY the JSON object as specified above. Do not include any additional text, numbering, or commentary outside the JSON structure.\n");
			sb.Append("Return the result as a valid JSON array. Do not include explanations, only raw JSON. [/INST]");

			return sb.ToString();
		}

		public static BrainDumpResponse? ParseBrainDumpResponse(string aiResponse, ILogger? logger = null)
		{
			try
			{
				logger?.LogInformation("Step 0 - Raw AI Response: {RawResponse}", aiResponse);

				// Step 1: Parse the RunPod envelope and extract text from output -> choices -> tokens
				// The incoming aiResponse is the raw JSON returned by RunPod.
				string extractedText = aiResponse;
				try
				{
					var runpod = JsonSerializer.Deserialize<RunpodResponse>(aiResponse, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
					if (runpod != null && runpod.Output != null && runpod.Output.Count > 0)
					{
						var tokens = runpod.Output
							.SelectMany(o => o.Choices ?? new())
							.SelectMany(c => c.Tokens ?? new())
							.ToList();
						extractedText = tokens.Count > 0 ? string.Join(string.Empty, tokens) : extractedText;
					}
				}
				catch
				{
					// If envelope parsing fails, fall back to treating input as plain text
					extractedText = aiResponse;
				}

				logger?.LogInformation("Step 1 - Extracted from RunPod tokens: {ExtractedText}", extractedText);

				// Step 2: Clean the extracted text - remove any markdown fencing and whitespace
				var cleanText = extractedText.Trim();
				if (cleanText.StartsWith("```json"))
				{
					cleanText = cleanText.Substring(7);
				}
				if (cleanText.EndsWith("```"))
				{
					cleanText = cleanText.Substring(0, cleanText.Length - 3);
				}
				cleanText = cleanText.Trim();

				logger?.LogInformation("Step 2 - After removing markdown fences: {CleanText}", cleanText);

				// Step 3: If the model prefixed text like "Here is the JSON...", extract only the JSON object
				if (!cleanText.StartsWith("{") && cleanText.Contains('{') && cleanText.Contains('}'))
				{
					var first = cleanText.IndexOf('{');
					var last = cleanText.LastIndexOf('}');
					if (first >= 0 && last > first)
					{
						cleanText = cleanText.Substring(first, last - first + 1).Trim();
					}
				}

				logger?.LogInformation("Step 3 - After extracting JSON object: {FinalText}", cleanText);

				// Step 4: Try to repair common JSON issues before deserialization
				var repairedText = RepairJson(cleanText);
				if (repairedText != cleanText)
				{
					logger?.LogInformation("Step 4a - JSON repaired: {RepairedText}", repairedText);
				}

				// Step 5: Deserialize into our domain response
				var response = JsonSerializer.Deserialize<BrainDumpResponse>(repairedText, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				logger?.LogInformation("Step 5 - Successfully deserialized BrainDumpResponse");
				return response;
			}
			catch (JsonException)
			{
				// Fallback: try to extract just the suggested activities if full parsing fails
				return CreateFallbackResponse(aiResponse);
			}
		}

		private static string RepairJson(string json)
		{
			try
			{

				// Deep repair with JsonRepairSharp
				var repaired = JsonRepair.RepairJson(json);
				return string.IsNullOrWhiteSpace(repaired) ? json : repaired;
			}
			catch
			{
				try
				{
					var fallback = JsonRepair.RepairJson(json);
					return string.IsNullOrWhiteSpace(fallback) ? json : fallback;
				}
				catch { return json; }
			}
		}

		private static BrainDumpResponse CreateFallbackResponse(string aiResponse)
		{
			// Extract task suggestions using the existing parser
			var suggestions = LlamaPromptBuilderForRunpod.ParseTaskSuggestions(aiResponse);
			
			return new BrainDumpResponse
			{
				UserProfile = new UserProfileSummary
				{
					Name = "User",
					CurrentState = "Processing",
					Emoji = "🤔"
				},
				KeyThemes = new List<string> { "General Wellness", "Personal Growth", "Self-Care" },
				AiSummary = "Your brain dump has been processed and personalized task suggestions have been generated based on your input.",
				SuggestedActivities = suggestions
			};
		}
	}
}



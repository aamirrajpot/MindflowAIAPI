using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using JsonRepairSharp;

namespace Mindflow_Web_API.Utilities
{
	public static class BrainDumpPromptBuilder
	{
        //public static string BuildTaskSuggestionsPrompt(BrainDumpRequest request, DTOs.WellnessCheckInDto? wellnessData = null, string? userName = null)
        //{
        //	var sb = new StringBuilder();
        //	sb.Append("[INST] You are a helpful assistant that analyzes brain dumps and provides comprehensive insights. ");
        //	sb.Append("You must return a JSON object with the following structure:\n\n");
        //	sb.Append("{\n");
        //	sb.Append("  \"userProfile\": {\n");
        //	sb.Append($"    \"name\": \"{userName ?? "User"}\",\n");
        //	sb.Append("    \"currentState\": \"Brief emotional state description (e.g., 'Reflective & Optimistic')\",\n");
        //	sb.Append("    \"emoji\": \"Relevant emoji (e.g., '😊', '🤔', '💪')\"\n");
        //	sb.Append("  },\n");
        //	sb.Append("  \"keyThemes\": [\"Theme 1\", \"Theme 2\", \"Theme 3\"],\n");
        //	sb.Append("  \"aiSummary\": \"Comprehensive 2-3 sentence summary of the user's emotional state and mindset\",\n");
        //	sb.Append("  \"suggestedActivities\": [\n");
        //	sb.Append("    {\n");
        //	sb.Append("      \"task\": \"Activity name\",\n");
        //	sb.Append("      \"frequency\": \"How often\",\n");
        //	sb.Append("      \"duration\": \"Time needed\",\n");
        //	sb.Append("      \"notes\": \"Why this helps and how to do it\"\n");
        //	sb.Append("    }\n");
        //	sb.Append("  ]\n");
        //	sb.Append("}\n\n");

        //	// PRIMARY FOCUS: Brain Dump Content
        //	sb.Append("=== USER BRAIN DUMP (PRIMARY ANALYSIS SOURCE) ===\n");
        //	sb.Append(request.Text.Replace("\r", "").Replace("\n", "\\n"));
        //	sb.Append("\n\n");

        //	// Additional context from brain dump
        //	if (!string.IsNullOrWhiteSpace(request.Context))
        //	{
        //		sb.Append("Additional Context from Brain Dump:\n");
        //		sb.Append(request.Context!.Replace("\r", "").Replace("\n", "\\n"));
        //		sb.Append("\n\n");
        //	}

        //	// Brain dump mood/stress/purpose scores (from the brain dump itself)
        //	if (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue)
        //	{
        //		sb.Append("Self-Reported Well-being from Brain Dump (0-10): ");
        //		if (request.Mood.HasValue) sb.Append($"Mood={request.Mood.Value} ");
        //		if (request.Stress.HasValue) sb.Append($"Stress={request.Stress.Value} ");
        //		if (request.Purpose.HasValue) sb.Append($"Purpose={request.Purpose.Value} ");
        //		sb.Append("\n\n");
        //	}

        //	// SECONDARY: Only time availability from wellness data
        //	if (wellnessData != null)
        //	{
        //		sb.Append("=== AVAILABLE TIME SLOTS (for task scheduling only) ===\n");

        //		// Only include time slots from wellness data
        //		if (!string.IsNullOrWhiteSpace(wellnessData.WeekdayStartTime) || !string.IsNullOrWhiteSpace(wellnessData.WeekendStartTime))
        //		{
        //			if (!string.IsNullOrWhiteSpace(wellnessData.WeekdayStartTime))
        //			{
        //				sb.Append($"- Weekdays: {wellnessData.WeekdayStartTime} {wellnessData.WeekdayStartShift} to {wellnessData.WeekdayEndTime} {wellnessData.WeekdayEndShift}\n");
        //			}
        //			if (!string.IsNullOrWhiteSpace(wellnessData.WeekendStartTime))
        //			{
        //				sb.Append($"- Weekends: {wellnessData.WeekendStartTime} {wellnessData.WeekendStartShift} to {wellnessData.WeekendEndTime} {wellnessData.WeekendEndShift}\n");
        //			}
        //			sb.Append("\n");
        //		}
        //	}

        //	sb.Append("=== ANALYSIS INSTRUCTIONS ===\n");
        //	sb.Append("PRIMARY FOCUS: Analyze the brain dump content above to understand the user's current state, concerns, and needs.\n\n");
        //	sb.Append("Analysis Guidelines:\n");
        //	sb.Append("- User Profile: Extract emotional state from the brain dump text (not from wellness data)\n");
        //	sb.Append("- Key Themes: Identify 3 main topics/concerns directly from the brain dump content\n");
        //	sb.Append("- AI Summary: Write a supportive summary based on what the user expressed in their brain dump\n");
        //	sb.Append("- Suggested Activities: 3-5 practical tasks that:\n");
        //	sb.Append("  * DIRECTLY address the concerns and themes mentioned in the brain dump\n");
        //	sb.Append("  * Are inspired by the user's own words and expressed needs\n");
        //	sb.Append("  * Consider available time slots (if provided) for realistic scheduling\n");
        //	sb.Append("  * Help with the specific issues the user mentioned in their brain dump\n");
        //	sb.Append("  * Are actionable and relevant to their current situation\n\n");

        //	sb.Append("IMPORTANT: Base your analysis primarily on the brain dump content. Use wellness time slots only for scheduling suggestions.\n");
        //	sb.Append("Return ONLY the JSON object as specified above. Do not include any additional text, numbering, or commentary outside the JSON structure.\n");
        //	sb.Append("Return the result as a valid JSON array. Do not include explanations, only raw JSON. [/INST]");

        //	return sb.ToString();
        //}

        public static string BuildTaskSuggestionsPrompt(
				BrainDumpRequest request,
				DTOs.WellnessCheckInDto? wellnessData = null,
				string? userName = null,
				bool forceMinimumActivities = false)
			{
				var sb = new StringBuilder();
				sb.Append("[INST] You are a warm, expert wellness coach and a careful extraction engine. ");
					sb.Append("Your job is to analyze the following brain dump *deeply* and exhaustively. ");
					sb.Append("First, IDENTIFY every explicit and implied action, obligation, decision, follow-up, appointment, errand, and emotional need mentioned in the text. ");
					sb.Append("For each found item, produce up to THREE plausible interpretations when the text is ambiguous. ");
					sb.Append("Then convert each interpretation into one or more concrete, realistic activities. ");
					sb.Append("Mark each activity as either \"explicit\" (directly stated) or \"inferred\" (reasonably implied). ");
					sb.Append("Also include for each inferred activity: (a) a low-friction micro-step and (b) a high-impact version (if applicable). ");
					sb.Append("Finally, present the final result AS A SINGLE JSON OBJECT with the structure below and nothing else.\n\n");

				sb.Append("{\n");
				sb.Append("  \"userProfile\": {\n");
				sb.Append($"    \"name\": \"{userName ?? "User"}\",\n");
				sb.Append("    \"currentState\": \"One short emotional state description based ONLY on the brain dump\",\n");
				sb.Append("    \"emoji\": \"One relevant emoji\"\n");
				sb.Append("  },\n");
				sb.Append("  \"keyThemes\": [\"Theme 1\", \"Theme 2\", \"Theme 3\"],\n");
				sb.Append("  \"aiSummary\": \"Empathetic 2–3 sentence summary capturing the user’s current mindset, needs, and emotional tone\",\n");
				sb.Append("  \"suggestedActivities\": [\n");
				sb.Append("    {\n");
				sb.Append("      \"task\": \"Short action title in second-person (plain text only, do NOT include priority keywords or extra commentary)\",\n");
				sb.Append("      \"frequency\": \"Use one of: 'once', 'daily', 'weekly', 'bi-weekly', 'monthly', 'weekdays', or 'never'\",\n");
				sb.Append("      \"duration\": \"Time needed (e.g., '10 minutes', '30 minutes')\",\n");
				sb.Append("      \"notes\": \"Explain to you why this task matters, referencing the user's own words (quote or paraphrase) without markdown\",\n");
				sb.Append("      \"priority\": \"High/Medium/Low - based on urgency and emotional importance\",\n");
				sb.Append("      \"suggestedTime\": \"Optional preferred time (e.g., 'Morning', 'After work', 'Evening')\"\n");
				sb.Append("    }\n");
				sb.Append("  ]\n");
				sb.Append("}\n\n");

				sb.Append("=== USER BRAIN DUMP (PRIMARY SOURCE) ===\n");
				sb.Append(request.Text.Replace("\r", "").Replace("\n", "\\n"));
				sb.Append("\n\n");
				sb.Append("Before producing activities, parse every sentence and list ALL concrete obligations, decisions, pending conversations, errands, deadlines, blockers, and emotional pain points you spot. Treat that list as a checklist that must be fully addressed in the JSON output.\n\n");

				if (!string.IsNullOrWhiteSpace(request.Context))
				{
					sb.Append("Additional Context:\n");
					sb.Append(request.Context!.Replace("\r", "").Replace("\n", "\\n"));
					sb.Append("\n\n");
				}

				if (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue)
				{
					sb.Append("Self-Reported Scores (0-10): ");
					if (request.Mood.HasValue) sb.Append($"Mood={request.Mood.Value} ");
					if (request.Stress.HasValue) sb.Append($"Stress={request.Stress.Value} ");
					if (request.Purpose.HasValue) sb.Append($"Purpose={request.Purpose.Value} ");
					sb.Append("\n\n");
				}

				sb.Append("=== INSTRUCTIONS ===\n");
				sb.Append("- Focus on understanding how you feel and what you might need right now.\n");
				sb.Append("- Base every activity on explicit statements from the brain dump (quote or paraphrase the trigger in the notes).\n");
				sb.Append("- Use second-person perspective: speak directly to you using \"you\"/\"your\". Do NOT use \"the user\".\n");
				sb.Append("- The `userProfile.name` value is already correct. Repeat it exactly; never replace it with \"You\".\n");
				sb.Append("- Example: task title \"Call the insurance adjuster\" instead of \"Help the user call...\".\n");
				sb.Append("- Task titles must stay concise (max 8 words) and action-oriented.\n");
				sb.Append("- Output at least 8 activities TOTAL whenever possible. At least 6 must be actionable items drawn from the brain dump. Wellness/self-care items are capped at 2 (never more than 3) and only allowed after all actionable items appear.\n");
				sb.Append("- Prioritize concrete actions mentioned or implied in the brain dump (tasks, follow-ups, appointments, errands).\n");
				sb.Append("- List all actionable, brain-dump-based tasks first in the suggestedActivities array, sorted by priority (High → Medium → Low) and still preserving the dump’s order inside each priority tier.\n");
				sb.Append("- Never merge multiple actions into one entry. If the brain dump mentions 'call insurance and schedule blood draw', those MUST be two separate activities, each with its own trigger quote.\n");
				sb.Append("- Only after all actionable items are listed, include up to two wellness or self-care activities, unless there are no actionable items.\n");
				sb.Append("- Do not mix wellness items with actionable tasks; actionable tasks always come first, wellness last.\n");
				sb.Append("- If the brain dump has N actionable items (N ≥ 3), ensure at least N activities directly address those items before adding wellness tasks.\n");
				sb.Append("- For wellness/supportive tasks, explain clearly how they help you manage or unblock something mentioned in the brain dump.\n");
				sb.Append("- Make every activity personal, specific, and natural — like advice from a caring friend, not generic guidance.\n");

				if (forceMinimumActivities)
				{
					sb.Append("- The user explicitly requested a full list; ensure there are at least 10 activities (minimum 8 actionable + 2 wellness max).\n");
				}
				sb.Append("- Avoid generic suggestions (like 'make a list') or vague ones (like 'take care of yourself'). Keep them actionable and human.\n");
				sb.Append("- Never invent commitments that weren't hinted at; stay grounded in the brain dump facts.\n");
				sb.Append("- Use the available time slots to make scheduling realistic but focus on the emotional fit first.\n");

				sb.Append("Output only the JSON object. Do not include any text outside JSON. [/INST]");

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

				// Step 3: Extract only the JSON object, handling cases where LLM adds extra text
				if (cleanText.Contains('{') && cleanText.Contains('}'))
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

		public static string BuildInsightPrompt(BrainDumpEntry entry, List<BrainDumpEntry> recentEntries)
		{
			var recentContext = recentEntries.Count > 0 
				? string.Join("\n", recentEntries.Take(5).Select(e => $"- {e.Text?.Substring(0, Math.Min(100, e.Text?.Length ?? 0))}..."))
				: "No recent entries";

			return $@"[INST] You are Mindflow AI, a wellness coach. Analyze this brain dump entry and provide a brief, insightful observation about patterns, emotions, or growth opportunities.

				Current Entry:
				Text: {entry.Text}
				Context: {entry.Context}
				Mood: {entry.Mood}/10
				Stress: {entry.Stress}/10
				Purpose: {entry.Purpose}/10

				Recent Context (last 30 days):
				{recentContext}

				Provide a single, concise insight (2-3 sentences max) that:
				- Identifies emotional patterns or themes
				- Offers gentle encouragement or perspective
				- Suggests a small actionable step if appropriate

				IMPORTANT: Return ONLY the insight text. Do not include any prefixes like Insight:, Here's, or explanatory text. Start directly with the insight content. [/INST]";
		}

		public static string ParseInsightResponse(string response, ILogger? logger = null)
		{
			try
			{
				// Clean up the response
				var cleanResponse = response?.Trim() ?? string.Empty;
				
				// If it's wrapped in code blocks, remove them
				if (cleanResponse.StartsWith("```") && cleanResponse.EndsWith("```"))
				{
					cleanResponse = cleanResponse.Substring(3, cleanResponse.Length - 6).Trim();
				}
				
				// Extract text from RunPod response envelope if present
				try
				{
					var runpod = JsonSerializer.Deserialize<RunpodResponse>(cleanResponse, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
					
					if (runpod?.Output?.Count > 0)
					{
						var tokens = runpod.Output
							.SelectMany(o => o.Choices ?? new())
							.SelectMany(c => c.Tokens ?? new())
							.ToList();
						
						if (tokens.Count > 0)
						{
							cleanResponse = string.Join(string.Empty, tokens);
						}
					}
				}
				catch
				{
					// If RunPod parsing fails, use the original response
				}
				
				// Remove common prefixes that LLMs sometimes add
				var prefixesToRemove = new[]
				{
					"insight:",
					"here's",
					"here is",
					"the insight is:",
					"insight is:",
					"based on",
					"it appears that",
					"i notice that",
					"i can see that"
				};
				
				foreach (var prefix in prefixesToRemove)
				{
					if (cleanResponse.ToLower().TrimStart().StartsWith(prefix))
					{
						cleanResponse = cleanResponse.Substring(prefix.Length+1).Trim();
						// Remove any leading punctuation or capitalization
						if (cleanResponse.StartsWith(":"))
							cleanResponse = cleanResponse.Substring(1).Trim();
						break;
					}
				}
				
				// Return the cleaned insight
				return cleanResponse.Length > 500 ? cleanResponse.Substring(0, 500) + "..." : cleanResponse;
			}
			catch (Exception ex)
			{
				logger?.LogWarning(ex, "Failed to parse insight response");
				return "Insight generation temporarily unavailable.";
			}
		}

		public static string BuildTagExtractionPrompt(string text)
		{
			return $@"[INST] You are an expert at analyzing text and extracting relevant tags. Analyze the following text and extract 3-5 meaningful tags that best represent the content, emotions, themes, or topics.

Text: {text}

Extract tags that capture:
- Emotional states (e.g., anxious, grateful, overwhelmed)
- Life areas (e.g., work, family, health, relationships)
- Activities or themes (e.g., meditation, exercise, planning, reflection)
- Time context (e.g., morning, evening, weekend)

IMPORTANT: Return ONLY a comma-separated list of tags. Do not include any explanatory text, introductions, or formatting. Start your response directly with the tags.

Example format: anxious,work,planning,morning [/INST]";
		}

        private static string? NormalizeUtcClockString(string? time, string? shift)
        {
            if (string.IsNullOrWhiteSpace(time)) return null;

            // If already 24h like "05:00" → return as-is
            if (TimeSpan.TryParse(time, out var ts))
            {
                return ts.ToString("hh\\:mm");
            }

            // If client passes AM/PM + optional shift, coerce to 24h
            var t = time.Trim();
            var upper = (shift ?? string.Empty).Trim().ToUpperInvariant();
            var isPM = upper.Contains("PM") || t.ToUpperInvariant().Contains("PM");
            var isAM = upper.Contains("AM") || t.ToUpperInvariant().Contains("AM");

            t = t.Replace("AM", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("PM", "", StringComparison.OrdinalIgnoreCase)
                 .Trim();

            if (!TimeSpan.TryParse(t, out var parsed)) return time; // fallback

            if (isPM && parsed.Hours >= 1 && parsed.Hours <= 12)
            {
                parsed = parsed.Add(new TimeSpan(12, 0, 0));
            }
            else if (isAM && parsed.Hours == 12)
            {
                parsed = parsed.Subtract(new TimeSpan(12, 0, 0));
            }

            return parsed.ToString("hh\\:mm");
        }

		public static string ParseTagExtractionResponse(string response, ILogger? logger = null)
		{
			try
			{
				// Clean up the response
				var cleanResponse = response?.Trim() ?? string.Empty;
				
				// If it's wrapped in code blocks, remove them
				if (cleanResponse.StartsWith("```") && cleanResponse.EndsWith("```"))
				{
					cleanResponse = cleanResponse.Substring(3, cleanResponse.Length - 6).Trim();
				}
				
				// Extract text from RunPod response envelope if present
				try
				{
					var runpod = JsonSerializer.Deserialize<RunpodResponse>(cleanResponse, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
					
					if (runpod?.Output?.Count > 0)
					{
						var tokens = runpod.Output
							.SelectMany(o => o.Choices ?? new())
							.SelectMany(c => c.Tokens ?? new())
							.ToList();
						
						if (tokens.Count > 0)
						{
							cleanResponse = string.Join(string.Empty, tokens);
						}
					}
				}
				catch
				{
					// If RunPod parsing fails, use the original response
				}
				
				// Remove explanatory text and extract only the tags
				// Look for common patterns that indicate the start of actual tags
				var tagStartPatterns = new[]
				{
					"here are",
					"the tags are:",
					"tags:",
					"meaningful tags",
					"relevant tags"
				};
				
				foreach (var pattern in tagStartPatterns)
				{
					var index = cleanResponse.ToLower().IndexOf(pattern);
					if (index >= 0)
					{
						// Find the colon or newline after the pattern
						var afterPattern = cleanResponse.Substring(index + pattern.Length);
						var colonIndex = afterPattern.IndexOf(':');
						var newlineIndex = afterPattern.IndexOf('\n');
						
						var startIndex = Math.Min(
							colonIndex >= 0 ? colonIndex + 1 : int.MaxValue,
							newlineIndex >= 0 ? newlineIndex + 1 : int.MaxValue
						);
						
						if (startIndex < int.MaxValue)
						{
							cleanResponse = afterPattern.Substring(startIndex).Trim();
							break;
						}
					}
				}
				
				// If we still have explanatory text, try to find the last line that looks like tags
				var lines = cleanResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
				var lastLine = lines.LastOrDefault()?.Trim();
				
				// Check if the last line looks like comma-separated tags (contains common tag words)
				if (!string.IsNullOrEmpty(lastLine) && lastLine.Contains(','))
				{
					cleanResponse = lastLine;
				}
				
				// Clean up the tags - remove extra whitespace and normalize
				var tags = cleanResponse
					.Split(',', StringSplitOptions.RemoveEmptyEntries)
					.Select(tag => tag.Trim().ToLower())
					.Where(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length > 1) // Filter out single characters
					.Take(5) // Limit to 5 tags max
					.ToList();
				
				return string.Join(",", tags);
			}
			catch (Exception ex)
			{
				logger?.LogWarning(ex, "Failed to parse tag extraction response");
				return string.Empty;
			}
		}
	}
}



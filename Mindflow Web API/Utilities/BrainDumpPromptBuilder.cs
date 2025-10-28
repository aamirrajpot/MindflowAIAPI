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

        public static string BuildTaskSuggestionsPrompt(BrainDumpRequest request, DTOs.WellnessCheckInDto? wellnessData = null, string? userName = null, bool forceMinimumActivities = false)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] You are a supportive wellness assistant that analyzes brain dumps and creates personalized insights and activities. ");
            sb.Append("Return your response STRICTLY as a valid JSON object with the following structure:\n\n");
            sb.Append("{\n");
            sb.Append("  \"userProfile\": {\n");
            sb.Append($"    \"name\": \"{userName ?? "User"}\",\n");
            sb.Append("    \"currentState\": \"One short emotional state description based ONLY on the brain dump\",\n");
            sb.Append("    \"emoji\": \"One relevant emoji\"\n");
            sb.Append("  },\n");
            sb.Append("  \"keyThemes\": [\"Theme 1\", \"Theme 2\", \"Theme 3\"],\n");
            sb.Append("  \"aiSummary\": \"Supportive 2–3 sentence summary of the user's mindset and needs\",\n");
            sb.Append("  \"suggestedActivities\": [\n");
            sb.Append("    {\n");
            sb.Append("      \"task\": \"Concrete activity (short phrase)\",\n");
            sb.Append("      \"frequency\": \"Realistic frequency (e.g., 'Once today', 'Daily')\",\n");
            sb.Append("      \"duration\": \"Time needed (e.g., '5 minutes', '15 minutes')\",\n");
            sb.Append("      \"notes\": \"Brief reason why this task helps, based on the user's brain dump\",\n");
            sb.Append("      \"priority\": \"High/Medium/Low - based on urgency and importance\",\n");
            sb.Append("      \"suggestedTime\": \"Optimal time within available slots (e.g., '9:00 AM', '2:30 PM')\"\n");
            sb.Append("    }\n");
            sb.Append("  ]\n");
            sb.Append("}\n\n");

            // Brain dump is the main input
            sb.Append("=== USER BRAIN DUMP ===\n");
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
                sb.Append("Self-Reported Scores (0-10): ");
                if (request.Mood.HasValue) sb.Append($"Mood={request.Mood.Value} ");
                if (request.Stress.HasValue) sb.Append($"Stress={request.Stress.Value} ");
                if (request.Purpose.HasValue) sb.Append($"Purpose={request.Purpose.Value} ");
                sb.Append("\n\n");
            }

            if (wellnessData != null)
            {
                // Convert US Eastern times to UTC for the prompt
                var wdStartUtc = ConvertUsEasternToUtcString(wellnessData.WeekdayStartTime, wellnessData.WeekdayStartShift);
                var wdEndUtc = ConvertUsEasternToUtcString(wellnessData.WeekdayEndTime, wellnessData.WeekdayEndShift);
                var weStartUtc = ConvertUsEasternToUtcString(wellnessData.WeekendStartTime, wellnessData.WeekendStartShift);
                var weEndUtc = ConvertUsEasternToUtcString(wellnessData.WeekendEndTime, wellnessData.WeekendEndShift);

                sb.Append("=== AVAILABLE TIME SLOTS (UTC, for realistic scheduling only) ===\n");
                if (!string.IsNullOrWhiteSpace(wdStartUtc) && !string.IsNullOrWhiteSpace(wdEndUtc))
                {
                    sb.Append($"- Weekdays (UTC): {wdStartUtc} to {wdEndUtc}\n");
                }
                if (!string.IsNullOrWhiteSpace(weStartUtc) && !string.IsNullOrWhiteSpace(weEndUtc))
                {
                    sb.Append($"- Weekends (UTC): {weStartUtc} to {weEndUtc}\n");
                }
                sb.Append("\n");
            }

            sb.Append("=== INSTRUCTIONS ===\n");
            sb.Append("- Extract emotional state ONLY from the brain dump (not wellness data).\n");
            sb.Append("- Key Themes must directly reflect concerns expressed in the brain dump (should be mostly of 2 words).\n");
            sb.Append("- Suggested Activities:\n");
            sb.Append("  * Identify ALL important and concrete tasks mentioned in the brain dump.\n");
            sb.Append("  * Include EVERY explicit task from the user's text as its own separate activity. Do not omit or merge them.\n");
            sb.Append("  * ALSO add wellness/self-care tasks based on Mood, Stress, and Purpose scores when needed.\n");
            sb.Append("  * Always order tasks so that higher-priority or urgent items appear first (e.g., health, time-sensitive, financial).\n");
            sb.Append("  * Do not merge multiple tasks into one generic suggestion — list them separately.\n");
            sb.Append("  * Do not invent meta-tasks such as 'make a to-do list' or 'organize your calendar' — only return actionable tasks that the user can directly do.\n");
            if (forceMinimumActivities)
            {
                sb.Append("  * Total: You MUST return 6–8 activities (no fewer than 6).\n");
                sb.Append("  * Prioritize including ALL explicit tasks from the brain dump first. If fewer than 6 explicit tasks exist, add focused wellness/self-care tasks to reach 6–8 total.\n");
            }
            sb.Append("  * Each activity must be written as a direct suggestion to the user (e.g., 'Call your mother', 'Donate clothes to Goodwill').\n");
            sb.Append("  * Keep each task short, specific, and realistic within available time slots.\n");
            sb.Append("  * TIME SCHEDULING (OPTIONAL):\n");
            sb.Append("    - Use suggestedTime to indicate optimal time preference (not required).\n");
            sb.Append("    - For suggestedTime, use: 'Morning', 'Afternoon', 'Evening', or specific times like '9:30 AM'.\n");
            sb.Append("    - Focus on task content and priority - scheduling will be handled automatically.\n");
            sb.Append("    - Only suggest time if it's important for the task type (e.g., 'Morning' for exercise).\n");


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

        /// <summary>
        /// Converts US Eastern Time to UTC string for AI prompt display.
        /// Assumes US Eastern Standard Time (UTC-5).
        /// </summary>
        private static string? ConvertUsEasternToUtcString(string? time, string? shift)
        {
            if (string.IsNullOrWhiteSpace(time)) return null;

            // Parse the time string (e.g., "3:00" + "PM")
            var t = time.Trim();
            var upper = (shift ?? string.Empty).Trim().ToUpperInvariant();
            var isPM = upper.Contains("PM") || t.ToUpperInvariant().Contains("PM");
            var isAM = upper.Contains("AM") || t.ToUpperInvariant().Contains("AM");

            t = t.Replace("AM", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("PM", "", StringComparison.OrdinalIgnoreCase)
                 .Trim();

            if (!TimeSpan.TryParse(t, out var easternTime)) return null;

            // Convert AM/PM to 24-hour format
            if (isPM && easternTime.Hours >= 1 && easternTime.Hours <= 12)
            {
                easternTime = easternTime.Add(new TimeSpan(12, 0, 0));
            }
            else if (isAM && easternTime.Hours == 12)
            {
                easternTime = easternTime.Subtract(new TimeSpan(12, 0, 0));
            }

            // Convert US Eastern to UTC (UTC-5 for EST)
            var utcOffset = TimeSpan.FromHours(5);
            var utcTime = easternTime.Add(utcOffset);

            // Handle day boundary crossing
            if (utcTime.TotalMinutes < 0)
            {
                utcTime = utcTime.Add(TimeSpan.FromDays(1));
            }
            else if (utcTime.TotalMinutes >= 1440) // 24 hours
            {
                utcTime = utcTime.Subtract(TimeSpan.FromDays(1));
            }

            return utcTime.ToString("hh\\:mm");
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



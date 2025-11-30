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

        // 		public static string BuildTaskSuggestionsPrompt(
        // 				BrainDumpRequest request,
        // 				DTOs.WellnessCheckInDto? wellnessData = null,
        // 				string? userName = null,
        // 				bool forceMinimumActivities = false)
        // 			{
        // 				/* ... previous prompt removed for brevity ... */
        // 			}

        //      public static string BuildTaskSuggestionsPrompt(
        //	BrainDumpRequest request,
        //	DTOs.WellnessCheckInDto? wellnessData = null,
        //	string? userName = null,
        //	bool forceMinimumActivities = false)
        //{
        //	var sb = new StringBuilder();
        //	var prompt = $@"[INST] You are a warm wellness coach and a precise action-extraction engine. 
        //				Your job is to analyze the user’s brain dump deeply.

        //				IMPORTANT RULES (READ FIRST):
        //				- You MUST return one actionable task for every actionable or implied item mentioned.
        //				- If the brain dump has N obligations, you MUST output at least N actionable tasks.
        //				- You MUST NOT merge tasks.
        //				- You MUST NOT drop tasks.
        //				- Do NOT summarize the tasks; list each one separately.
        //				- Keep outputs concrete and actionable.
        //				- Do NOT sort tasks; keep them in the order they appear.

        //				OUTPUT FORMAT:
        //				Return ONLY a single JSON object:
        //				{{
        //					""userProfile"": {{
        //					""name"": ""{userName ?? "User"}"",
        //					""currentState"": ""Short emotional state"",
        //					""emoji"": ""Emoji""
        //					}},
        //					""keyThemes"": [""Theme 1"", ""Theme 2"", ""Theme 3""],
        //					""aiSummary"": ""Empathetic 2–3 sentence summary"",
        //					""suggestedActivities"": [
        //					{{
        //						""task"": ""Short action title"",
        //						""frequency"": ""once | daily | weekly | bi-weekly | monthly | weekdays | never"",
        //						""duration"": ""Concrete duration"",
        //						""notes"": ""A short reason based directly on the brain dump. Do NOT include the phrase 'Paraphrased trigger' or anything similar."",
        //						""priority"": ""High | Medium | Low"",
        //						""suggestedTime"": ""Morning | Afternoon | Evening | specific time""
        //					}}
        //					]
        //				}}

        //				=== USER BRAIN DUMP (SOURCE) ===
        //				{request.Text.Replace("\r", "").Replace("\n", "\\n")}

        //				Instructions:
        //				- Identify every explicit or implied action, follow-up, decision, conversation, appointment, errand, or emotional need.
        //				- For ambiguous statements, internally consider multiple interpretations, but output only the final tasks.
        //				- Generate one activity per actionable point. Do not compress tasks.
        //				- After all actionable items, you may add up to 2 wellness tasks.
        //				- Fill all fields. No empty values.

        //				ADDITIONAL HARD RULES:
        //				- The ""task"" field must contain ONLY the short action title. Never begin it with ""Task:"" or include other fields in it.
        //				- The ""notes"" field must contain ONLY a paraphrase of the trigger from the brain dump. 
        //				  Never include priority, suggestedTime, or frequency in notes.
        //				- Never leave any field blank. Fill frequency, duration, priority, and suggestedTime based on the most reasonable assumption.
        //				- Do NOT place commentary or explanations after the JSON output.
        //				- Do NOT label any tasks as ""Wellness Task"". Just write them as normal tasks.
        //				- Do NOT embed long text, emojis, or sentences in suggestedTime. Only ""Morning"", ""Afternoon"", ""Evening"", or specific times.
        //				- Ever

        //				Return only JSON. [/INST]";

        //	sb.Append(prompt);
        //	return sb.ToString();
        //}

        //    public static string BuildTaskSuggestionsPrompt(
        //BrainDumpRequest request,
        //DTOs.WellnessCheckInDto? wellnessData = null,
        //string? userName = null,
        //bool forceMinimumActivities = false)
        //    {
        //        var sb = new StringBuilder();

        //        sb.Append("[INST] You are a warm, expert wellness coach and an accurate extraction engine. ");
        //        sb.Append("Your job is to read the user's brain dump and extract EVERY actionable item without skipping anything. ");
        //        sb.Append("This includes tasks, errands, calls, follow-ups, decisions, appointments, and implied obligations.\n\n");

        //        sb.Append("IMPORTANT REQUIREMENT:\n");
        //        sb.Append("→ You MUST return at least one task for every actionable or implied point mentioned in the brain dump.\n");
        //        sb.Append("→ If the brain dump contains 14 items, you must return at least 14 tasks. Never fewer.\n");
        //        sb.Append("→ Never merge multiple tasks into one. Split at every 'and', 'also', or separate verb.\n\n");

        //        sb.AppendLine("SAMPLE OUTPUT FORMAT:");
        //        sb.AppendLine("Return ONLY a single JSON object:");
        //        sb.AppendLine("{");
        //        sb.AppendLine("    \"userProfile\": {");
        //        sb.AppendLine($"        \"name\": \"{userName ?? "User"}\",");
        //        sb.AppendLine("        \"currentState\": \"Analyze the user's emotional state based on the brain dump, 1–2 words or a short phrase\",");
        //        sb.AppendLine("        \"emoji\": \"Select one emoji representing the user's current mood\"");
        //        sb.AppendLine("    },");
        //        sb.AppendLine("    \"keyThemes\": [\"Analyze 3 key themes or topics mentioned in the brain dump\"],");
        //        sb.AppendLine("    \"aiSummary\": \"Generate a 2–3 sentence empathetic summary describing the user's mindset, needs, and emotional tone\"");
        //        sb.AppendLine("    \"suggestedActivities\": [");
        //        sb.AppendLine("        {");
        //        sb.AppendLine("            \"task\": \"Short action title\",");
        //        sb.AppendLine("            \"frequency\": \"once | daily | weekly | bi-weekly | monthly | weekdays | never\",");
        //        sb.AppendLine("            \"duration\": \"Concrete duration\",");
        //        sb.AppendLine("            \"notes\": \"Short reason directly from the user's text\",");
        //        sb.AppendLine("            \"priority\": \"High | Medium | Low\",");
        //        sb.AppendLine("            \"suggestedTime\": \"Morning | Afternoon | Evening | specific time\"");
        //        sb.AppendLine("        }");
        //        sb.AppendLine("    ]");
        //        sb.AppendLine("}");


        //        sb.Append("Duration Rules:\n");
        //        sb.Append("- The duration field must contain ONLY a time estimate in minutes or hours.\n");
        //        sb.Append("- Valid examples: \"10 minutes\", \"20 minutes\", \"45 minutes\", \"1 hour\", \"2 hours\".\n");
        //        sb.Append("- NEVER include sentences, actions, or extra words.\n\n");

        //        sb.Append("Notes Rules:\n");
        //        sb.Append("- Notes must be a short, natural explanation referencing the source text.\n");
        //        sb.Append("- Example: \"You said you need to call the insurance company about a check.\"\n");
        //        sb.Append("- Do NOT use template phrases like: 'Paraphrased trigger', 'Trigger phrase', 'From brain dump'.\n");
        //        sb.Append("- Keep it personal, warm, and human.\n\n");

        //        sb.Append("Task Ordering Rules:\n");
        //        sb.Append("- List tasks in the same order they appear in the brain dump.\n");
        //        sb.Append("- Priority distribution must vary (not all Medium).\n");
        //        sb.Append("- Actionable tasks first, optional wellness tasks last (max 2 wellness tasks).\n\n");

        //        if (forceMinimumActivities)
        //            sb.Append("- Because the user requested a full list, return at least 12 total tasks.\n\n");

        //        sb.Append("=== USER BRAIN DUMP ===\n");
        //        sb.Append(request.Text.Replace("\r", "").Replace("\n", "\\n"));
        //        sb.Append("\n\n");

        //        if (!string.IsNullOrWhiteSpace(request.Context))
        //        {
        //            sb.Append("Additional Context:\\n");
        //            sb.Append(request.Context!.Replace("\r", "").Replace("\n", "\\n"));
        //            sb.Append("\n\n");
        //        }

        //        if (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue)
        //        {
        //            sb.Append("Self-Reported Scores (0-10): ");
        //            if (request.Mood.HasValue) sb.Append($"Mood={request.Mood.Value} ");
        //            if (request.Stress.HasValue) sb.Append($"Stress={request.Stress.Value} ");
        //            if (request.Purpose.HasValue) sb.Append($"Purpose={request.Purpose.Value} ");
        //            sb.Append("\n\n");
        //        }

        //        sb.Append("Return ONLY the JSON array. No description. No commentary. No text outside JSON.\n");
        //        sb.Append("\nLINKING RULES:\n");
        //        sb.Append("- Every keyTheme you output MUST have at least one matching task in suggestedActivities that addresses it.\n");
        //        sb.Append("- If you list 9 themes or obligations, you must return at least 9 actionable tasks (one per theme, plus extra tasks if a theme implies multiple steps).\n");
        //        sb.Append("- Do not output more themes than tasks. Tasks and themes must correspond 1:1 or 1:many (never fewer tasks than themes).\n");
        //        sb.Append("\n[/INST]");

        //        return sb.ToString();
        //    }
        // Multi-prompt approach: Step 1 - Extract Key Themes
        public static string BuildExtractThemesPrompt(string summary, List<string> emotions, List<string> topics)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are analyzing a user's brain dump. Extract exactly 3 key themes or main topics.\n\n");
            sb.Append("Summary: ");
            sb.Append(summary);
            sb.Append("\n\n");
            sb.Append("Emotions: ");
            sb.Append(string.Join(", ", emotions));
            sb.Append("\n\n");
            sb.Append("Topics: ");
            sb.Append(string.Join(", ", topics));
            sb.Append("\n\n");
            sb.Append("Return ONLY a JSON array with exactly 3 theme strings.\n");
            sb.Append("Example: [\"Work stress\", \"Family time\", \"Health goals\"]\n");
            sb.Append("No explanations. Just the JSON array. [/INST]");
            return sb.ToString();
        }

        // Multi-prompt approach: Step 2 - Generate User Profile (Enhanced)
        public static string BuildUserProfilePrompt(string originalText, string summary, List<string> emotions, string? userName = null)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are analyzing a user's brain dump to determine their current emotional state. Based on the text, emotions, and context, create a personalized profile.\n\n");
            
            sb.Append("CRITICAL: You MUST return ONLY valid JSON. No text before or after. No explanations.\n\n");
            
            sb.Append("Original Text (for context):\n");
            sb.Append(originalText.Length > 500 ? originalText.Substring(0, 500) + "..." : originalText);
            sb.Append("\n\n");
            
            sb.Append("Summary: ");
            sb.Append(summary);
            sb.Append("\n\n");
            
            sb.Append("Detected Emotions: ");
            sb.Append(string.Join(", ", emotions));
            sb.Append("\n\n");
            
            sb.Append("Return ONLY this exact JSON structure (replace the example values):\n");
            sb.Append("{\n");
            sb.Append($"  \"name\": \"{userName ?? "User"}\",\n");
            sb.Append("  \"currentState\": \"2-3 word emotional state like 'Reflective & Optimistic' or 'Stressed & Overwhelmed' or 'Grateful & Energized'\",\n");
            sb.Append("  \"emoji\": \"one emoji that matches the emotional state like 😊 or 😔 or 🤔 or 💪\"\n");
            sb.Append("}\n\n");
            
            sb.Append("EXAMPLES:\n");
            sb.Append("If user is stressed: {\"name\": \"User\", \"currentState\": \"Stressed & Overwhelmed\", \"emoji\": \"😰\"}\n");
            sb.Append("If user is grateful: {\"name\": \"User\", \"currentState\": \"Grateful & Reflective\", \"emoji\": \"😊\"}\n");
            sb.Append("If user is anxious: {\"name\": \"User\", \"currentState\": \"Anxious & Uncertain\", \"emoji\": \"😟\"}\n\n");
            
            sb.Append("IMPORTANT: Return ONLY the JSON object. No markdown. No code blocks. No explanations. Start with { and end with }. [/INST]");
            return sb.ToString();
        }

        // Multi-prompt approach: Step 3 - Generate AI Summary (Enhanced)
        public static string BuildAiSummaryPrompt(string originalText, string summary, List<string> emotions, List<string> themes, BrainDumpRequest? request = null)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are a warm, therapy-informed wellness coach. Write a personalized, insightful summary that deeply reflects the user's emotional and practical state.\n\n");
            
            sb.Append("CRITICAL RULES:\n");
            sb.Append("- DO NOT repeat words unnecessarily (avoid saying 'overwhelmed' multiple times)\n");
            sb.Append("- Focus on the DEEPER MEANING behind what they're expressing\n");
            sb.Append("- Include SPECIFIC examples from their text (reference actual details they mentioned)\n");
            sb.Append("- Use therapy-informed language: validate their feelings, acknowledge their experience\n");
            sb.Append("- Be warm and supportive, not clinical or robotic\n");
            sb.Append("- Write 2-3 sentences that feel personal and meaningful\n");
            sb.Append("- Avoid generic phrases like 'it seems like' or 'you appear to'\n");
            sb.Append("- Connect their emotions to their practical concerns when relevant\n\n");
            
            sb.Append("Original Text (for context and specific details):\n");
            sb.Append(originalText);
            sb.Append("\n\n");
            
            sb.Append("Extracted Summary:\n");
            sb.Append(summary);
            sb.Append("\n\n");
            
            sb.Append("Detected Emotions: ");
            sb.Append(string.Join(", ", emotions));
            sb.Append("\n\n");
            
            sb.Append("Key Themes: ");
            sb.Append(string.Join(", ", themes));
            sb.Append("\n\n");
            
            // Add self-reported scores if available for context
            if (request != null && (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue))
            {
                sb.Append("Self-Reported Well-being Scores:\n");
                if (request.Mood.HasValue) sb.Append($"- Mood: {request.Mood.Value}/10\n");
                if (request.Stress.HasValue) sb.Append($"- Stress: {request.Stress.Value}/10\n");
                if (request.Purpose.HasValue) sb.Append($"- Purpose: {request.Purpose.Value}/10\n");
                sb.Append("\n");
            }
            
            sb.Append("EXAMPLE OF GOOD SUMMARY:\n");
            sb.Append("\"You're navigating a lot right now - work deadlines are piling up, and you're feeling the weight of trying to balance everything. The fact that you're thinking about talking to your manager shows self-awareness, and it's understandable that you're feeling behind when juggling multiple priorities.\"\n\n");
            
            sb.Append("EXAMPLE OF BAD SUMMARY (avoid this):\n");
            sb.Append("\"You seem overwhelmed and stressed about work. You have deadlines and feel overwhelmed. It seems like you need to manage your time better.\"\n\n");
            
            sb.Append("Now write a personalized, insightful summary that:\n");
            sb.Append("1. Validates their experience without being repetitive\n");
            sb.Append("2. References specific details they mentioned\n");
            sb.Append("3. Shows you understand the deeper meaning\n");
            sb.Append("4. Uses warm, supportive, therapy-informed language\n\n");
            
            sb.Append("Return ONLY the summary text. No JSON. No quotes. No prefixes like 'Summary:' or 'Here's'. Start directly with the summary content. [/INST]");
            return sb.ToString();
        }

        // Multi-prompt approach: Step 4 - Generate Task Suggestions
        public static string BuildTaskSuggestionsPrompt(
            string summary,
            List<string> emotions,
            List<string> topics,
            List<string> themes,
            WellnessSummary wellness,
            BrainDumpRequest request,
            bool forceMinimumActivities = false)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are a wellness coach. Generate actionable task suggestions based on the provided information.\n\n");

            sb.Append("Summary: ");
            sb.Append(summary);
            sb.Append("\n\n");

            sb.Append("Themes: ");
            sb.Append(string.Join(", ", themes));
            sb.Append("\n\n");

            sb.Append("Emotions: ");
            sb.Append(string.Join(", ", emotions));
            sb.Append("\n\n");

            sb.Append("Topics: ");
            sb.Append(string.Join(", ", topics));
            sb.Append("\n\n");

            // Add wellness summary (minimal)
            sb.Append("Wellness Profile:\n");
            sb.Append($"Mood: {wellness.MoodLevel ?? "not specified"}\n");
            if (wellness.FocusAreas.Any())
                sb.Append($"Focus Areas: {string.Join(", ", wellness.FocusAreas)}\n");
            if (wellness.PreferredTimeBlocks.Any())
                sb.Append($"Preferred Times: {string.Join(", ", wellness.PreferredTimeBlocks)}\n");
            sb.Append("\n");

            // Self-reported scores
            if (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue)
            {
                sb.Append("Self-Reported Scores: ");
                if (request.Mood.HasValue) sb.Append($"Mood={request.Mood.Value} ");
                if (request.Stress.HasValue) sb.Append($"Stress={request.Stress.Value} ");
                if (request.Purpose.HasValue) sb.Append($"Purpose={request.Purpose.Value} ");
                sb.Append("\n\n");
            }

            sb.Append("Return ONLY a JSON array of task objects:\n");
            sb.Append("[\n");
            sb.Append("  {\n");
            sb.Append("    \"task\": \"short actionable step\",\n");
            sb.Append("    \"frequency\": \"once | daily | weekly | bi-weekly | monthly | weekdays\",\n");
            sb.Append("    \"duration\": \"10 minutes | 20 minutes | 30 minutes | 1 hour\",\n");
            sb.Append("    \"notes\": \"short explanation tied to themes\",\n");
            sb.Append("    \"priority\": \"High | Medium | Low\",\n");
            sb.Append("    \"suggestedTime\": \"Morning | Afternoon | Evening | specific time\"\n");
            sb.Append("  }\n");
            sb.Append("]\n\n");

            sb.Append("RULES:\n");
            sb.Append("- Generate at least one task per theme.\n");
            sb.Append("- Use ONLY information provided. No hallucinations.\n");
            sb.Append("- Keep tasks practical and actionable.\n");
            if (forceMinimumActivities)
                sb.Append("- Return AT LEAST 12 tasks total.\n");
            sb.Append("\nReturn ONLY the JSON array. No commentary. [/INST]");

            return sb.ToString();
        }

        // Legacy method kept for backward compatibility (can be removed later)
        public static string BuildTaskSuggestionsPrompt(
    string summary,
    List<string> emotions,
    List<string> topics,
    WellnessSummary wellness,
    BrainDumpRequest request,
    string? userName = null,
    bool forceMinimumActivities = false)
        {
            // This is now a wrapper that calls the multi-prompt approach
            // For backward compatibility, we'll extract themes first, then build the full response
            // But ideally, callers should use the new multi-prompt methods directly
            var themes = new List<string> { "General", "Wellness", "Personal" }; // Fallback themes
            return BuildTaskSuggestionsPrompt(summary, emotions, topics, themes, wellness, request, forceMinimumActivities);
        }



        // Parser for Step 1: Extract Themes
        public static List<string> ParseThemesResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                var extractedText = ExtractTextFromRunPodResponse(aiResponse, logger);
                var cleanText = CleanJsonText(extractedText, logger);
                
                var themes = JsonSerializer.Deserialize<List<string>>(cleanText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return themes ?? new List<string>();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse themes response");
                return new List<string> { "General", "Wellness", "Personal" };
            }
        }

        // Parser for Step 2: User Profile (Enhanced)
        public static UserProfileSummary ParseUserProfileResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    logger?.LogWarning("User profile response is null or empty");
                    return new UserProfileSummary { Name = "User", CurrentState = "Processing", Emoji = "🤔" };
                }
                
                logger?.LogDebug("Parsing user profile response. Raw response length: {Length}", aiResponse.Length);
                
                var extractedText = ExtractTextFromRunPodResponse(aiResponse, logger);
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    logger?.LogWarning("Extracted text is null or empty");
                    return new UserProfileSummary { Name = "User", CurrentState = "Processing", Emoji = "🤔" };
                }
                
                logger?.LogDebug("Extracted text from RunPod response: {Text}", extractedText.Substring(0, Math.Min(200, extractedText.Length)));
                
                var cleanText = CleanJsonText(extractedText, logger);
                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    logger?.LogWarning("Cleaned text is null or empty");
                    return new UserProfileSummary { Name = "User", CurrentState = "Processing", Emoji = "🤔" };
                }
                
                logger?.LogDebug("Cleaned JSON text: {Text}", cleanText.Substring(0, Math.Min(200, cleanText.Length)));
                
                // Try to extract JSON object if wrapped in text
                if (cleanText.Contains('{') && cleanText.Contains('}'))
                {
                    var firstBrace = cleanText.IndexOf('{');
                    var lastBrace = cleanText.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        cleanText = cleanText.Substring(firstBrace, lastBrace - firstBrace + 1);
                    }
                }
                
                // Try to repair JSON if needed
                try
                {
                    cleanText = RepairJson(cleanText);
                }
                catch
                {
                    // If repair fails, continue with original
                }
                
                var profile = JsonSerializer.Deserialize<UserProfileSummary>(cleanText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });
                
                // Validate the parsed profile
                if (profile != null && 
                    !string.IsNullOrWhiteSpace(profile.CurrentState) && 
                    profile.CurrentState != "Processing" &&
                    !string.IsNullOrWhiteSpace(profile.Emoji))
                {
                    logger?.LogDebug("Successfully parsed user profile: {State}, {Emoji}", profile.CurrentState, profile.Emoji);
                    return profile;
                }
                
                logger?.LogWarning("Parsed profile is invalid or has default values. Clean text was: {Text}", cleanText);
                return new UserProfileSummary { Name = "User", CurrentState = "Processing", Emoji = "🤔" };
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse user profile response. Response was: {Response}", aiResponse?.Substring(0, Math.Min(500, aiResponse?.Length ?? 0)));
                return new UserProfileSummary { Name = "User", CurrentState = "Processing", Emoji = "🤔" };
            }
        }

        // Parser for Step 3: AI Summary (Enhanced)
        public static string ParseAiSummaryResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                var extractedText = ExtractTextFromRunPodResponse(aiResponse, logger);
                var cleanText = extractedText.Trim();
                
                // Remove any markdown or code blocks
                if (cleanText.StartsWith("```"))
                {
                    var lines = cleanText.Split('\n');
                    if (lines.Length > 2)
                        cleanText = string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
                    else
                        cleanText = cleanText.Replace("```", "").Trim();
                }
                
                // Remove quotes if present
                if (cleanText.StartsWith("\"") && cleanText.EndsWith("\""))
                    cleanText = cleanText.Substring(1, cleanText.Length - 2);
                
                // Remove common prefixes that LLMs sometimes add
                var prefixesToRemove = new[]
                {
                    "summary:",
                    "here's the summary:",
                    "here is the summary:",
                    "summary:",
                    "the summary is:",
                    "based on your text,",
                    "based on what you wrote,",
                    "looking at your brain dump,",
                    "from your entry,",
                    "here's what i understand:",
                    "here is what i understand:"
                };
                
                var lowerText = cleanText.ToLower().TrimStart();
                foreach (var prefix in prefixesToRemove)
                {
                    if (lowerText.StartsWith(prefix))
                    {
                        cleanText = cleanText.Substring(prefix.Length).TrimStart();
                        // Remove any leading colon or dash
                        if (cleanText.StartsWith(":") || cleanText.StartsWith("-"))
                            cleanText = cleanText.Substring(1).TrimStart();
                        break;
                    }
                }
                
                // Ensure we have meaningful content (at least 20 characters)
                if (cleanText.Length < 20)
                {
                    logger?.LogWarning("Summary too short, using fallback");
                    return "Your brain dump has been processed and personalized insights have been generated.";
                }
                
                return cleanText.Trim();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse AI summary response");
                return "Your brain dump has been processed and personalized insights have been generated.";
            }
        }

        // Multi-prompt approach: Step 5 - Break Down Tasks into Micro-Steps
        public static string BuildTaskBreakdownPrompt(List<TaskSuggestion> tasks, string originalText)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are a productivity coach. Break down complex tasks into 2-3 actionable micro-steps.\n\n");
            
            sb.Append("CRITICAL RULES:\n");
            sb.Append("- Only break down tasks that are complex or multi-step\n");
            sb.Append("- Simple tasks (like 'Take a 10-minute walk') don't need breakdown\n");
            sb.Append("- Each micro-step should be specific and actionable\n");
            sb.Append("- Micro-steps should be in logical order\n");
            sb.Append("- Return sub-steps only for tasks that need them\n\n");
            
            sb.Append("Original Brain Dump Context:\n");
            sb.Append(originalText.Length > 500 ? originalText.Substring(0, 500) + "..." : originalText);
            sb.Append("\n\n");
            
            sb.Append("Tasks to analyze:\n");
            for (int i = 0; i < tasks.Count; i++)
            {
                sb.Append($"{i + 1}. {tasks[i].Task}\n");
                if (!string.IsNullOrWhiteSpace(tasks[i].Notes))
                    sb.Append($"   Notes: {tasks[i].Notes}\n");
            }
            sb.Append("\n");
            
            sb.Append("Return ONLY a JSON object where keys are task numbers (1, 2, 3...) and values are arrays of sub-steps.\n");
            sb.Append("Only include tasks that need breakdown. Skip simple tasks.\n\n");
            
            sb.Append("EXAMPLE:\n");
            sb.Append("{\n");
            sb.Append("  \"1\": [\"Research moving companies online\", \"Get quotes from 3 companies\", \"Compare prices and services\"],\n");
            sb.Append("  \"3\": [\"Schedule doctor appointment\", \"Prepare list of symptoms\", \"Write down questions to ask\"]\n");
            sb.Append("}\n\n");
            
            sb.Append("If task 1 is \"Pack kitchen items\" and it's complex, include it.\n");
            sb.Append("If task 2 is \"Take a 10-minute walk\" and it's simple, skip it.\n\n");
            
            sb.Append("Return ONLY the JSON object. No explanations. No markdown. Start with { and end with }. [/INST]");
            return sb.ToString();
        }

        // Multi-prompt approach: Step 6 - Generate Emotional Intelligence Layer
        public static string BuildEmotionalIntelligencePrompt(
            string originalText,
            string summary,
            List<string> emotions,
            List<string> themes,
            BrainDumpRequest? request = null)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are a warm, therapy-informed wellness coach. Provide emotional intelligence insights that validate, acknowledge, and support the user.\n\n");
            
            sb.Append("CRITICAL: Use therapy-informed language - warm, supportive, validating, non-clinical.\n\n");
            
            sb.Append("Original Text (for context):\n");
            sb.Append(originalText.Length > 600 ? originalText.Substring(0, 600) + "..." : originalText);
            sb.Append("\n\n");
            
            sb.Append("Summary: ");
            sb.Append(summary);
            sb.Append("\n\n");
            
            sb.Append("Detected Emotions: ");
            sb.Append(string.Join(", ", emotions));
            sb.Append("\n\n");
            
            sb.Append("Key Themes: ");
            sb.Append(string.Join(", ", themes));
            sb.Append("\n\n");
            
            // Add self-reported scores if available
            if (request != null && (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue))
            {
                sb.Append("Self-Reported Scores:\n");
                if (request.Mood.HasValue) sb.Append($"- Mood: {request.Mood.Value}/10\n");
                if (request.Stress.HasValue) sb.Append($"- Stress: {request.Stress.Value}/10\n");
                if (request.Purpose.HasValue) sb.Append($"- Purpose: {request.Purpose.Value}/10\n");
                sb.Append("\n");
            }
            
            sb.Append("Return ONLY a JSON object with these three fields:\n");
            sb.Append("{\n");
            sb.Append("  \"emotionalValidation\": \"2-3 sentences that validate and acknowledge their feelings. Use phrases like 'It makes sense that...', 'I hear that...', 'It's understandable that...'\",\n");
            sb.Append("  \"patternInsight\": \"1-2 sentences naming a pattern or theme you notice. Be specific and insightful, not generic.\",\n");
            sb.Append("  \"copingTools\": [\"One quick coping strategy (1-2 sentences)\", \"Another coping strategy (1-2 sentences)\"]\n");
            sb.Append("}\n\n");
            
            sb.Append("EXAMPLES:\n\n");
            sb.Append("Example 1 (Stressed about work):\n");
            sb.Append("{\n");
            sb.Append("  \"emotionalValidation\": \"It makes sense that you're feeling overwhelmed with multiple deadlines. The pressure of trying to balance everything can be really taxing, and it's understandable that you're feeling behind.\",\n");
            sb.Append("  \"patternInsight\": \"I notice you're juggling several priorities at once, which often leads to feeling stretched thin. This pattern suggests you might benefit from clearer boundaries around your workload.\",\n");
            sb.Append("  \"copingTools\": [\"Take a 5-minute breathing break: Inhale for 4 counts, hold for 4, exhale for 6. This activates your body's relaxation response.\", \"Try the '2-minute rule': If something takes less than 2 minutes, do it now. This can help clear small tasks that add to mental clutter.\"]\n");
            sb.Append("}\n\n");
            
            sb.Append("Example 2 (Relationship concerns):\n");
            sb.Append("{\n");
            sb.Append("  \"emotionalValidation\": \"I hear that you're feeling uncertain about your relationship, and that's a vulnerable place to be. It's understandable that communication challenges can feel isolating.\",\n");
            sb.Append("  \"patternInsight\": \"I notice you're recognizing the need for better communication, which shows self-awareness. This pattern often emerges when both people are trying but missing each other's signals.\",\n");
            sb.Append("  \"copingTools\": [\"Before your next conversation, write down what you want to say and what you need. This helps clarify your thoughts and reduces reactive responses.\", \"Practice active listening: When your partner speaks, try to reflect back what you heard before responding. This creates space for understanding.\"]\n");
            sb.Append("}\n\n");
            
            sb.Append("RULES:\n");
            sb.Append("- emotionalValidation: Validate their experience, acknowledge their feelings, use warm language\n");
            sb.Append("- patternInsight: Name a specific pattern you notice, be insightful not generic\n");
            sb.Append("- copingTools: Provide 1-2 practical, actionable coping strategies (each as a string)\n");
            sb.Append("- Keep it warm, supportive, and therapy-informed\n");
            sb.Append("- Avoid clinical jargon or diagnostic language\n");
            sb.Append("- Be specific to their situation, not generic advice\n\n");
            
            sb.Append("Return ONLY the JSON object. No markdown. No explanations. Start with { and end with }. [/INST]");
            return sb.ToString();
        }

        // Parser for Step 6: Emotional Intelligence
        public static (string? EmotionalValidation, string? PatternInsight, List<string>? CopingTools) ParseEmotionalIntelligenceResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    logger?.LogWarning("Emotional intelligence response is null or empty");
                    return (null, null, null);
                }
                
                var extractedText = ExtractTextFromRunPodResponse(aiResponse, logger);
                logger?.LogDebug("Extracted emotional intelligence text: {Text}", extractedText?.Substring(0, Math.Min(300, extractedText?.Length ?? 0)));
                
                var cleanText = extractedText?.Trim() ?? string.Empty;
                
                // Remove markdown code blocks
                if (cleanText.StartsWith("```json"))
                    cleanText = cleanText.Substring(7);
                if (cleanText.StartsWith("```"))
                    cleanText = cleanText.Substring(3);
                if (cleanText.EndsWith("```"))
                    cleanText = cleanText.Substring(0, cleanText.Length - 3);
                cleanText = cleanText.Trim();
                
                // Extract JSON object
                if (cleanText.Contains('{') && cleanText.Contains('}'))
                {
                    var firstBrace = cleanText.IndexOf('{');
                    var lastBrace = cleanText.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        cleanText = cleanText.Substring(firstBrace, lastBrace - firstBrace + 1);
                    }
                }
                else
                {
                    logger?.LogWarning("No JSON object found in emotional intelligence response");
                    return (null, null, null);
                }
                
                // Try to repair JSON
                try
                {
                    cleanText = RepairJson(cleanText);
                }
                catch
                {
                    // If repair fails, continue with original
                }
                
                // Parse JSON
                using var doc = JsonDocument.Parse(cleanText);
                var root = doc.RootElement;
                
                var emotionalValidation = root.TryGetProperty("emotionalValidation", out var validationEl) 
                    ? validationEl.GetString() 
                    : null;
                
                var patternInsight = root.TryGetProperty("patternInsight", out var patternEl) 
                    ? patternEl.GetString() 
                    : null;
                
                List<string>? copingTools = null;
                if (root.TryGetProperty("copingTools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
                {
                    copingTools = new List<string>();
                    foreach (var tool in toolsEl.EnumerateArray())
                    {
                        if (tool.ValueKind == JsonValueKind.String)
                        {
                            var toolText = tool.GetString();
                            if (!string.IsNullOrWhiteSpace(toolText))
                                copingTools.Add(toolText);
                        }
                    }
                }
                
                logger?.LogDebug("Parsed emotional intelligence: Validation={HasValidation}, Pattern={HasPattern}, Tools={ToolsCount}", 
                    !string.IsNullOrWhiteSpace(emotionalValidation), 
                    !string.IsNullOrWhiteSpace(patternInsight), 
                    copingTools?.Count ?? 0);
                
                return (emotionalValidation, patternInsight, copingTools);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse emotional intelligence response");
                return (null, null, null);
            }
        }

        // Parser for Step 5: Task Breakdown
        public static Dictionary<int, List<string>> ParseTaskBreakdownResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    logger?.LogWarning("Task breakdown response is null or empty");
                    return new Dictionary<int, List<string>>();
                }
                
                var extractedText = ExtractTextFromRunPodResponse(aiResponse, logger);
                logger?.LogDebug("Extracted text before cleaning: {Text}", extractedText?.Substring(0, Math.Min(300, extractedText?.Length ?? 0)));
                
                var cleanText = extractedText?.Trim() ?? string.Empty;
                
                // Remove markdown code blocks first
                if (cleanText.StartsWith("```json"))
                    cleanText = cleanText.Substring(7);
                if (cleanText.StartsWith("```"))
                    cleanText = cleanText.Substring(3);
                if (cleanText.EndsWith("```"))
                    cleanText = cleanText.Substring(0, cleanText.Length - 3);
                cleanText = cleanText.Trim();
                
                // CRITICAL: Extract JSON OBJECT first (not array) - prioritize { over [
                // We need the object wrapper, not the inner arrays
                if (cleanText.Contains('{') && cleanText.Contains('}'))
                {
                    var firstBrace = cleanText.IndexOf('{');
                    var lastBrace = cleanText.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        cleanText = cleanText.Substring(firstBrace, lastBrace - firstBrace + 1);
                        logger?.LogDebug("Extracted JSON object: {Text}", cleanText.Substring(0, Math.Min(300, cleanText.Length)));
                    }
                }
                else
                {
                    logger?.LogWarning("No JSON object found in response");
                    return new Dictionary<int, List<string>>();
                }
                
                // Try to repair JSON
                try
                {
                    cleanText = RepairJson(cleanText);
                }
                catch
                {
                    // If repair fails, continue with original
                }
                
                // Parse as dictionary with string keys (task numbers) and list of strings (sub-steps)
                var breakdown = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(cleanText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });
                
                if (breakdown == null)
                    return new Dictionary<int, List<string>>();
                
                // Convert string keys to int keys
                var result = new Dictionary<int, List<string>>();
                foreach (var kvp in breakdown)
                {
                    if (int.TryParse(kvp.Key, out int taskIndex))
                    {
                        // Convert to 0-based index (task 1 = index 0)
                        result[taskIndex - 1] = kvp.Value ?? new List<string>();
                    }
                }
                
                logger?.LogDebug("Parsed task breakdown for {Count} tasks", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse task breakdown response");
                return new Dictionary<int, List<string>>();
            }
        }

        // Parser for Step 4: Task Suggestions
        public static List<TaskSuggestion> ParseTaskSuggestionsResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                var extractedText = ExtractTextFromRunPodResponse(aiResponse, logger);
                var cleanText = CleanJsonText(extractedText, logger);
                
                var tasks = JsonSerializer.Deserialize<List<TaskSuggestion>>(cleanText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return tasks ?? new List<TaskSuggestion>();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse task suggestions response");
                return new List<TaskSuggestion>();
            }
        }

        // Helper method to extract text from RunPod response
        private static string ExtractTextFromRunPodResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                var runpod = JsonSerializer.Deserialize<RunpodResponse>(aiResponse, new JsonSerializerOptions
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
                        return string.Join(string.Empty, tokens);
                }
            }
            catch
            {
                // If parsing fails, return original response
            }
            
            return aiResponse;
        }

        // Helper method to clean JSON text
        private static string CleanJsonText(string text, ILogger? logger = null)
        {
            var cleanText = text.Trim();
            
            // Remove markdown code blocks
            if (cleanText.StartsWith("```json"))
                cleanText = cleanText.Substring(7);
            if (cleanText.StartsWith("```"))
                cleanText = cleanText.Substring(3);
            if (cleanText.EndsWith("```"))
                cleanText = cleanText.Substring(0, cleanText.Length - 3);
            
            cleanText = cleanText.Trim();
            
            // Extract JSON object/array if wrapped in text
            if (cleanText.Contains('[') && cleanText.Contains(']'))
            {
                var first = cleanText.IndexOf('[');
                var last = cleanText.LastIndexOf(']');
                if (first >= 0 && last > first)
                    cleanText = cleanText.Substring(first, last - first + 1);
            }
            else if (cleanText.Contains('{') && cleanText.Contains('}'))
            {
                var first = cleanText.IndexOf('{');
                var last = cleanText.LastIndexOf('}');
                if (first >= 0 && last > first)
                    cleanText = cleanText.Substring(first, last - first + 1);
            }
            
            // Try to repair JSON
            try
            {
                cleanText = RepairJson(cleanText);
            }
            catch
            {
                // If repair fails, use original
            }
            
            return cleanText;
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



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

        // Multi-prompt approach: Step 3 - Generate AI Summary (Enhanced - Therapist-Style with CBT/DBT/Trauma-Informed)
        public static string BuildAiSummaryPrompt(string originalText, string summary, List<string> emotions, List<string> themes, BrainDumpRequest? request = null)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are an experienced therapist providing a guided, conversational response to a client's brain dump. ");
            sb.Append("Your response should feel like a real therapist conversation - warm, reflective, validating, and gently guiding. ");
            sb.Append("This is NOT a simple summary. This is a back-and-forth therapeutic conversation starter.\n\n");
            
            // Determine which therapeutic approach to use
            var approach = DetermineTherapeuticApproach(originalText, emotions, themes, null);
            sb.Append($"THERAPEUTIC APPROACH: {approach}\n\n");
            
            if (approach == "CBT")
            {
                sb.Append("CBT (Cognitive Behavioral Therapy) STYLE:\n");
                sb.Append("- Focus on the connection between thoughts, feelings, and behaviors\n");
                sb.Append("- Help identify negative or unhelpful thoughts\n");
                sb.Append("- Gently guide toward examining evidence for and against thoughts\n");
                sb.Append("- Use structured, logical, thought-focused language\n");
                sb.Append("- Example: 'It sounds like you're having the thought that you're failing at everything. ");
                sb.Append("Let's pause and examine that—what evidence supports this thought, and what evidence might challenge it?'\n\n");
            }
            else if (approach == "DBT")
            {
                sb.Append("DBT (Dialectical Behavior Therapy) STYLE:\n");
                sb.Append("- Emotion-first approach - validate feelings FIRST\n");
                sb.Append("- Focus on emotional regulation and distress tolerance\n");
                sb.Append("- Use mindfulness language\n");
                sb.Append("- Validate without judging or dismissing\n");
                sb.Append("- Example: 'That sounds really overwhelming. Anyone in your situation would feel drained. ");
                sb.Append("Let's slow down for a moment—can you name what emotion feels strongest right now?'\n\n");
            }
            else if (approach == "TraumaInformed")
            {
                sb.Append("TRAUMA-INFORMED APPROACH STYLE:\n");
                sb.Append("- Prioritize emotional and psychological safety\n");
                sb.Append("- Avoid judgment or triggering language\n");
                sb.Append("- Empower the user (no 'you should' language)\n");
                sb.Append("- Give control back to the user\n");
                sb.Append("- Example: 'Thank you for sharing this. You're not required to fix anything right now. ");
                sb.Append("We can move at whatever pace feels safe for you.'\n\n");
            }
            else
            {
                sb.Append("ADAPTIVE THERAPEUTIC STYLE:\n");
                sb.Append("- Blend validation, reflection, and gentle guidance\n");
                sb.Append("- Use warm, empathetic language\n");
                sb.Append("- Include reflective questions that invite deeper exploration\n\n");
            }
            
            sb.Append("CRITICAL RULES:\n");
            sb.Append("- DO NOT repeat words unnecessarily (avoid saying 'overwhelmed' multiple times)\n");
            sb.Append("- Focus on the DEEPER MEANING behind what they're expressing\n");
            sb.Append("- Include SPECIFIC examples from their text (reference actual details they mentioned)\n");
            sb.Append("- Use therapy-informed language: validate their feelings, acknowledge their experience\n");
            sb.Append("- Be warm and supportive, not clinical or robotic\n");
            sb.Append("- Write 3-5 sentences that feel personal and meaningful\n");
            sb.Append("- Include 1-2 gentle, reflective questions that invite deeper exploration\n");
            sb.Append("- Avoid generic phrases like 'it seems like' or 'you appear to'\n");
            sb.Append("- Connect their emotions to their practical concerns when relevant\n");
            sb.Append("- This is a CONVERSATION STARTER, not just a summary - engage with their experience therapeutically\n");
            sb.Append("- Do NOT provide generic encouragement - be specific to their situation\n");
            sb.Append("- Do NOT list tasks - this is about emotional processing, not task management\n\n");
            
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
            
            sb.Append("GOOD EXAMPLE (CBT-style):\n");
            sb.Append("It sounds like you're having the thought that you're failing at everything and can't focus. ");
            sb.Append("Let's pause and examine that—what evidence supports this thought, and what evidence might challenge it? ");
            sb.Append("I notice you mentioned feeling behind on work deadlines. When you think about 'failing at everything,' ");
            sb.Append("are there areas where you're actually making progress, even if small? ");
            sb.Append("What would it feel like to acknowledge both the struggle and any steps you've taken, however small?\n\n");
            
            sb.Append("GOOD EXAMPLE (DBT-style):\n");
            sb.Append("That sounds really overwhelming. Anyone in your situation would feel drained trying to balance ");
            sb.Append("everything you've described. Let's slow down for a moment—can you name what emotion feels strongest ");
            sb.Append("right now? Is it the anxiety about deadlines, or maybe something deeper like fear of disappointing others? ");
            sb.Append("Sometimes naming the emotion helps us understand what we actually need in this moment.\n\n");
            
            sb.Append("BAD EXAMPLE (avoid this - too generic):\n");
            sb.Append("You seem overwhelmed and stressed about work. You have deadlines and feel overwhelmed. ");
            sb.Append("It seems like you need to manage your time better. Here are some tasks to help you.\n\n");
            
            sb.Append("CRITICAL OUTPUT RULES:\n");
            sb.Append("- Return ONLY the summary text itself\n");
            sb.Append("- DO NOT include any introductory phrases like 'Sure, here is...', 'Here's a summary...', 'Here is a personalized...', etc.\n");
            sb.Append("- DO NOT include phrases like 'that validates the user's experience' or 'that references specific details'\n");
            sb.Append("- DO NOT include quotes around the text\n");
            sb.Append("- DO NOT include JSON formatting\n");
            sb.Append("- Start DIRECTLY with the summary content (e.g., 'You're navigating...' or 'It sounds like...')\n");
            sb.Append("- The summary should:\n");
            sb.Append("  1. Validate their experience without being repetitive\n");
            sb.Append("  2. Reference specific details they mentioned\n");
            sb.Append("  3. Show you understand the deeper meaning\n");
            sb.Append("  4. Use warm, supportive, therapy-informed language\n\n");
            
            sb.Append("Return ONLY the summary text. Start directly with the summary content. [/INST]");
            return sb.ToString();
        }

        // Multi-prompt approach: Step 4 - Generate Task Suggestions
        public static string BuildTaskSuggestionsPrompt(
            string originalText,
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
            sb.Append("You are a wellness coach. Extract and generate SPECIFIC, actionable task suggestions from the user's brain dump.\n\n");

            sb.Append("CRITICAL: Your PRIMARY goal is to extract SPECIFIC tasks that are EXPLICITLY mentioned in the original text.\n");
            sb.Append("Prioritize exact tasks with full context over generic suggestions.\n\n");

            sb.Append("Original Text (READ CAREFULLY for specific tasks):\n");
            sb.Append(originalText);
            sb.Append("\n\n");

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
            sb.Append("    \"task\": \"SPECIFIC actionable task with full context\",\n");
            sb.Append("    \"frequency\": \"once | daily | weekly | bi-weekly | monthly | weekdays\",\n");
            sb.Append("    \"duration\": \"10 minutes | 20 minutes | 30 minutes | 1 hour\",\n");
            sb.Append("    \"notes\": \"short explanation tied to themes\",\n");
            sb.Append("    \"priority\": \"High | Medium | Low\",\n");
            sb.Append("    \"suggestedTime\": \"Morning | Afternoon | Evening | specific time\",\n");
            sb.Append("    \"urgency\": \"Low | Medium | High\",\n");
            sb.Append("    \"importance\": \"Low | Medium | High\",\n");
            sb.Append("    \"priorityScore\": 1-10\n");
            sb.Append("  }\n");
            sb.Append("]\n\n");

            sb.Append("EXTRACTION RULES (PRIORITY ORDER):\n");
            sb.Append("1. FIRST: Extract SPECIFIC tasks explicitly mentioned in the original text.\n");
            sb.Append("   - Include ALL context: names, places, specific items, deadlines.\n");
            sb.Append("   - Example: \"Call Dr. Smith about test results\" NOT \"Call doctor\"\n");
            sb.Append("   - Example: \"Pack kitchen items into labeled boxes\" NOT \"organize belongings\"\n");
            sb.Append("   - Example: \"Email Sarah about the project deadline on Friday\" NOT \"Send email\"\n\n");
            sb.Append("2. SECOND: Break complex task mentions into separate, specific tasks.\n");
            sb.Append("   - If text says \"I need to clean the garage and organize my office\", create TWO tasks.\n");
            sb.Append("   - Each task should be specific: \"Clean garage and sort items into keep/donate/trash\" and \"Organize office desk and file important documents\"\n\n");
            sb.Append("3. THIRD: Only if no explicit tasks found, infer actionable tasks from themes/emotions.\n");
            sb.Append("   - Even inferred tasks should be specific and actionable.\n");
            sb.Append("   - Example: If theme is \"work stress\", suggest \"Review workload and prioritize top 3 tasks for tomorrow\" NOT \"reduce stress\"\n\n");
            sb.Append("4. ALWAYS: Include actionable details that make the task clear and executable.\n");
            sb.Append("   - Specify what, where, when, or who when mentioned in the text.\n");
            sb.Append("   - Make tasks concrete, not abstract.\n\n");

            sb.Append("PRIORITIZATION RULES (URGENCY & IMPORTANCE):\n");
            sb.Append("- Urgency = how time-sensitive the task is.\n");
            sb.Append("  - HIGH: Has a deadline within a few days, is blocking something important, or user sounds very stressed about timing.\n");
            sb.Append("  - MEDIUM: Should be done soon but not today; some time pressure.\n");
            sb.Append("  - LOW: No clear deadline; can be done whenever.\n");
            sb.Append("- Importance = how impactful the task is on the user's life, health, work, or relationships.\n");
            sb.Append("  - HIGH: Affects health, job security, core relationships, or major life events (moving, finances, legal, medical).\n");
            sb.Append("  - MEDIUM: Helpful for stability, progress, or well-being but not critical.\n");
            sb.Append("  - LOW: Nice-to-have, optional, or minor convenience.\n");
            sb.Append("- priorityScore: 1-10 where higher = more urgent AND more important.\n");
            sb.Append("  - Example mapping:\n");
            sb.Append("    - High urgency + High importance => 9-10\n");
            sb.Append("    - High importance + Medium urgency => 7-8\n");
            sb.Append("    - Medium importance + Medium urgency => 5-6\n");
            sb.Append("    - Medium importance + Low urgency => 3-4\n");
            sb.Append("    - Low importance + Low urgency => 1-2\n\n");

            sb.Append("GENERAL RULES:\n");
            sb.Append("- Generate at least one task per theme.\n");
            sb.Append("- Use ONLY information from the original text. No hallucinations.\n");
            sb.Append("- Keep tasks practical, specific, and immediately actionable.\n");
            sb.Append("- Preserve all specific details (names, places, items, dates) from the original text.\n");
            if (forceMinimumActivities)
                sb.Append("- Return AT LEAST 12 tasks total.\n");
            
            sb.Append("\n");
            sb.Append("CRITICAL OUTPUT FORMAT:\n");
            sb.Append("You MUST return ONLY a valid JSON array. Do NOT include:\n");
            sb.Append("- Any introductory text like \"Here are...\" or \"The tasks are...\"\n");
            sb.Append("- Numbered lists or bullet points\n");
            sb.Append("- Explanations, summaries, or commentary\n");
            sb.Append("- Priority score lists or additional formatting\n");
            sb.Append("- Any text before or after the JSON array\n\n");
            
            sb.Append("CORRECT FORMAT EXAMPLE:\n");
            sb.Append("[\n");
            sb.Append("  {\"task\": \"Call Dr. Smith about test results\", \"frequency\": \"once\", \"duration\": \"10 minutes\", \"notes\": \"Schedule call to discuss results\", \"priority\": \"High\", \"suggestedTime\": \"Morning\", \"urgency\": \"High\", \"importance\": \"Medium\", \"priorityScore\": 9},\n");
            sb.Append("  {\"task\": \"Pack kitchen items into labeled boxes\", \"frequency\": \"weekly\", \"duration\": \"30 minutes\", \"notes\": \"Organize for moving\", \"priority\": \"Medium\", \"suggestedTime\": \"Afternoon\", \"urgency\": \"Low\", \"importance\": \"Medium\", \"priorityScore\": 7}\n");
            sb.Append("]\n\n");
            
            sb.Append("INCORRECT FORMAT (DO NOT DO THIS):\n");
            sb.Append("Here are ten specific tasks...\n");
            sb.Append("1. Task: ...\n");
            sb.Append("Priority score for each task: ...\n\n");
            
            sb.Append("Your response must start with [ and end with ]. Nothing else. [/INST]");

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
            var originalText = request.Text ?? string.Empty; // Use request text as original text
            return BuildTaskSuggestionsPrompt(originalText, summary, emotions, topics, themes, wellness, request, forceMinimumActivities);
        }



        // Parser for Step 1: Extract Themes
        public static List<string> ParseThemesResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
                var cleanText = CleanJsonText(extractedText, logger);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                // Try to deserialize as nested array first (handles cases where AI returns array of arrays)
                try
                {
                    var nestedThemes = JsonSerializer.Deserialize<List<List<string>>>(cleanText, options);
                    if (nestedThemes != null && nestedThemes.Count > 0)
                    {
                        // Flatten the nested array into a single list
                        var flattened = nestedThemes.SelectMany(x => x).Distinct().ToList();
                        logger?.LogDebug("Parsed nested themes array, flattened to {Count} themes", flattened.Count);
                        return flattened;
                    }
                }
                catch
                {
                    // Not a nested array, try flat array
                }
                
                // Try to deserialize as flat array
                var themes = JsonSerializer.Deserialize<List<string>>(cleanText, options);
                
                return themes ?? new List<string>();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse themes response: {Error}", ex.Message);
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
                
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
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
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
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
                
                // First, try to extract content from quotes if the entire response is quoted
                if (cleanText.StartsWith("\"") && cleanText.EndsWith("\""))
                    cleanText = cleanText.Substring(1, cleanText.Length - 2).Trim();
                
                // Remove common prefixes that LLMs sometimes add
                // Use case-insensitive matching and remove the longest matching prefix
                var prefixesToRemove = new[]
                {
                    "sure, here is a personalized, insightful summary that validates the user's experience and references specific details they mentioned:",
                    "sure, here's a personalized, insightful summary that validates the user's experience and references specific details they mentioned:",
                    "here is a personalized, insightful summary that validates the user's experience and references specific details they mentioned:",
                    "here's a personalized, insightful summary that validates the user's experience and references specific details they mentioned:",
                    "sure, here is a personalized, insightful summary:",
                    "sure, here's a personalized, insightful summary:",
                    "here is a personalized, insightful summary:",
                    "here's a personalized, insightful summary:",
                    "sure, here is the summary:",
                    "sure, here's the summary:",
                    "here is the summary:",
                    "here's the summary:",
                    "summary:",
                    "the summary is:",
                    "based on your text,",
                    "based on what you wrote,",
                    "looking at your brain dump,",
                    "from your entry,",
                    "here's what i understand:",
                    "here is what i understand:",
                    "sure, here is",
                    "sure, here's",
                    "here is",
                    "here's"
                };
                
                // Sort by length descending to match longest prefix first
                var sortedPrefixes = prefixesToRemove.OrderByDescending(p => p.Length).ToArray();
                var lowerText = cleanText.ToLower().TrimStart();
                
                foreach (var prefix in sortedPrefixes)
                {
                    if (lowerText.StartsWith(prefix))
                    {
                        cleanText = cleanText.Substring(prefix.Length).TrimStart();
                        // Remove any leading colon, dash, or quote
                        while (cleanText.Length > 0 && (cleanText[0] == ':' || cleanText[0] == '-' || cleanText[0] == '"' || cleanText[0] == '\''))
                            cleanText = cleanText.Substring(1).TrimStart();
                        break;
                    }
                }
                
                // Additional pattern: Look for introductory phrases ending with colon followed by quoted text
                // Pattern: "...summary:" or "...summary that..." followed by quote
                var quotePattern = new System.Text.RegularExpressions.Regex(@"^[^""]*[""'](.+)[""']\s*$", System.Text.RegularExpressions.RegexOptions.Singleline);
                var quoteMatch = quotePattern.Match(cleanText);
                if (quoteMatch.Success && quoteMatch.Groups.Count > 1)
                {
                    cleanText = quoteMatch.Groups[1].Value.Trim();
                }
                
                // If text still starts with common introductory patterns, try to find where actual summary starts
                // Look for patterns like "that validates" or "that references" and remove everything before the quote
                var introPattern = new System.Text.RegularExpressions.Regex(@".*?(?:validates|references|mentioned)[^""]*[""'](.+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                var introMatch = introPattern.Match(cleanText);
                if (introMatch.Success && introMatch.Groups.Count > 1)
                {
                    cleanText = introMatch.Groups[1].Value.Trim();
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

        // Multi-prompt approach: Step 6 - Generate Emotional Intelligence Layer (Enhanced with CBT/DBT/Trauma-Informed)
        public static string BuildEmotionalIntelligencePrompt(
            string originalText,
            string summary,
            List<string> emotions,
            List<string> themes,
            BrainDumpRequest? request = null)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are an experienced therapist providing emotional intelligence insights using evidence-based therapeutic approaches. ");
            sb.Append("Your responses should validate, acknowledge, and support the user using appropriate therapeutic techniques.\n\n");
            
            // Determine which therapeutic approach to use
            var approach = DetermineTherapeuticApproach(originalText, emotions, themes, null);
            sb.Append($"THERAPEUTIC APPROACH: {approach}\n\n");
            
            if (approach == "CBT")
            {
                sb.Append("CBT (Cognitive Behavioral Therapy) FOCUS:\n");
                sb.Append("- emotionalValidation: Focus on identifying the thought-feeling-behavior connection. ");
                sb.Append("Validate their experience while gently pointing to the thought patterns.\n");
                sb.Append("- patternInsight: Name cognitive patterns (e.g., 'all-or-nothing thinking', 'catastrophizing'). ");
                sb.Append("Be specific about how thoughts influence feelings.\n");
                sb.Append("- copingTools: Provide cognitive restructuring techniques, thought challenging exercises.\n\n");
            }
            else if (approach == "DBT")
            {
                sb.Append("DBT (Dialectical Behavior Therapy) FOCUS:\n");
                sb.Append("- emotionalValidation: Validate emotions FIRST. Use phrases like 'It makes sense that...', ");
                sb.Append("'Anyone would feel...', 'Your feelings are valid.'\n");
                sb.Append("- patternInsight: Focus on emotional patterns, distress tolerance, emotional regulation challenges.\n");
                sb.Append("- copingTools: Provide distress tolerance skills, mindfulness exercises, emotion regulation techniques.\n\n");
            }
            else if (approach == "TraumaInformed")
            {
                sb.Append("TRAUMA-INFORMED APPROACH FOCUS:\n");
                sb.Append("- emotionalValidation: Prioritize safety and control. Use gentle, non-directive language. ");
                sb.Append("Avoid 'you should' statements. Empower the user.\n");
                sb.Append("- patternInsight: Be careful with pattern identification - focus on strengths and resilience, ");
                sb.Append("not just challenges. Avoid retraumatizing language.\n");
                sb.Append("- copingTools: Provide grounding techniques, safety-building strategies, self-compassion exercises.\n\n");
            }
            else
            {
            sb.Append("ADAPTIVE THERAPEUTIC APPROACH:\n");
            sb.Append("- Blend validation, reflection, and gentle guidance\n");
            sb.Append("- Use warm, empathetic language appropriate to the situation\n\n");
            }
            
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
            sb.Append("  \"emotionalValidation\": \"2-3 sentences that validate and acknowledge their feelings using the therapeutic approach specified above\",\n");
            sb.Append("  \"patternInsight\": \"1-2 sentences naming a pattern or theme you notice, using the therapeutic approach specified above\",\n");
            sb.Append("  \"copingTools\": [\"One quick coping strategy aligned with the therapeutic approach (1-2 sentences)\", \"Another coping strategy (1-2 sentences)\"]\n");
            sb.Append("}\n\n");

            if (approach == "CBT")
            {
                sb.Append("CBT EXAMPLE (Stressed about work):\n");
                sb.Append("{\n");
                sb.Append("  \"emotionalValidation\": \"It sounds like you're having the thought that you're failing at everything, and that thought is creating a lot of distress. Let's examine what's really happening here - you mentioned specific deadlines and feeling behind, which suggests this isn't just a vague worry but something concrete you're navigating.\",\n");
                sb.Append("  \"patternInsight\": \"I notice a pattern of 'all-or-nothing thinking' here - when you say 'failing at everything,' it suggests you might be viewing your situation in black-and-white terms. This cognitive pattern often amplifies feelings of overwhelm.\",\n");
                sb.Append("  \"copingTools\": [\"Thought record exercise: Write down the thought 'I'm failing at everything.' Then list evidence FOR this thought and evidence AGAINST it. This helps challenge cognitive distortions.\", \"Behavioral experiment: Identify one small task you can complete today. Notice what happens to your 'failing' thought when you complete it.\"]\n");
                sb.Append("}\n\n");
            }
            else if (approach == "DBT")
            {
                sb.Append("DBT EXAMPLE (Overwhelmed with emotions):\n");
                sb.Append("{\n");
                sb.Append("  \"emotionalValidation\": \"That sounds really overwhelming, and anyone in your situation would feel drained. Your feelings are completely valid - it makes sense that trying to balance everything you've described would leave you feeling this way. Let's slow down for a moment and honor what you're experiencing.\",\n");
                sb.Append("  \"patternInsight\": \"I notice you're experiencing intense emotions that feel hard to manage right now. This pattern of emotional overwhelm often happens when we're trying to process multiple stressors at once without emotional regulation tools.\",\n");
                sb.Append("  \"copingTools\": [\"TIPP technique: Try cold water on your face or hold an ice cube. This activates the dive reflex and can help regulate intense emotions quickly.\", \"Mindfulness of current emotion: Name the emotion you're feeling right now (anxiety, sadness, anger?). Just observe it without judgment for 2 minutes.\"]\n");
                sb.Append("}\n\n");
            }
            else if (approach == "TraumaInformed")
            {
                sb.Append("TRAUMA-INFORMED EXAMPLE:\n");
                sb.Append("{\n");
                sb.Append("  \"emotionalValidation\": \"Thank you for sharing this with me. You're not required to fix anything right now, and it's okay that things feel overwhelming. What you're experiencing makes sense given what you've shared, and you have full control over how we proceed.\",\n");
                sb.Append("  \"patternInsight\": \"I notice you're showing resilience in even being able to name what's difficult. That takes strength. There's no pressure to identify patterns right now - we can move at whatever pace feels safe.\",\n");
                sb.Append("  \"copingTools\": [\"Grounding technique: Name 5 things you can see, 4 things you can touch, 3 things you can hear, 2 things you can smell, 1 thing you can taste. This helps anchor you in the present moment.\", \"Self-compassion break: Place a hand on your heart and say 'This is a moment of difficulty. It's okay to feel this way. I'm here with myself.'\"]\n");
                sb.Append("}\n\n");
            }
            else
            {
                sb.Append("ADAPTIVE EXAMPLE (Stressed about work):\n");
                sb.Append("{\n");
                sb.Append("  \"emotionalValidation\": \"It makes sense that you're feeling overwhelmed with multiple deadlines. The pressure of trying to balance everything can be really taxing, and it's understandable that you're feeling behind.\",\n");
                sb.Append("  \"patternInsight\": \"I notice you're juggling several priorities at once, which often leads to feeling stretched thin. This pattern suggests you might benefit from clearer boundaries around your workload.\",\n");
                sb.Append("  \"copingTools\": [\"Take a 5-minute breathing break: Inhale for 4 counts, hold for 4, exhale for 6. This activates your body's relaxation response.\", \"Try the '2-minute rule': If something takes less than 2 minutes, do it now. This can help clear small tasks that add to mental clutter.\"]\n");
                sb.Append("}\n\n");
            }
            
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

        // Step 7: Generate Therapist-Style Response (CBT/DBT/Trauma-Informed)
        public static string BuildTherapeuticResponsePrompt(
            string originalText,
            string summary,
            List<string> emotions,
            List<string> themes,
            BrainDumpRequest? request = null,
            string? preferredApproach = null)
        {
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append("You are an experienced therapist providing a guided, conversational response to a client's brain dump. ");
            sb.Append("Your response should feel like a real therapist conversation - warm, reflective, validating, and gently guiding. ");
            sb.Append("This is NOT a summary. This is a back-and-forth therapeutic conversation starter.\n\n");
            
            // Determine which therapeutic approach to use
            var approach = DetermineTherapeuticApproach(originalText, emotions, themes, preferredApproach);
            sb.Append($"THERAPEUTIC APPROACH: {approach}\n\n");
            
            if (approach == "CBT")
            {
                sb.Append("CBT (Cognitive Behavioral Therapy) FOCUS:\n");
                sb.Append("- Focus on the connection between thoughts, feelings, and behaviors\n");
                sb.Append("- Help identify negative or unhelpful thoughts\n");
                sb.Append("- Gently guide toward examining evidence for and against thoughts\n");
                sb.Append("- Use structured, logical, thought-focused language\n");
                sb.Append("- Ask reflective questions that help them examine their thinking\n");
                sb.Append("- Example style: 'It sounds like you're having the thought that you're failing at everything. ");
                sb.Append("Let's pause and examine that—what evidence supports this thought, and what evidence might challenge it?'\n\n");
            }
            else if (approach == "DBT")
            {
                sb.Append("DBT (Dialectical Behavior Therapy) FOCUS:\n");
                sb.Append("- Emotion-first approach - validate feelings FIRST\n");
                sb.Append("- Focus on emotional regulation and distress tolerance\n");
                sb.Append("- Use mindfulness language\n");
                sb.Append("- Validate without judging or dismissing\n");
                sb.Append("- Be calming and emotion-focused\n");
                sb.Append("- Example style: 'That sounds really overwhelming. Anyone in your situation would feel drained. ");
                sb.Append("Let's slow down for a moment—can you name what emotion feels strongest right now?'\n\n");
            }
            else // Trauma-Informed
            {
                sb.Append("TRAUMA-INFORMED APPROACH FOCUS:\n");
                sb.Append("- Prioritize emotional and psychological safety\n");
                sb.Append("- Avoid judgment or triggering language\n");
                sb.Append("- Empower the user (no 'you should' language)\n");
                sb.Append("- Give control back to the user\n");
                sb.Append("- Be very gentle, respectful, and non-directive\n");
                sb.Append("- Example style: 'Thank you for sharing this. You're not required to fix anything right now. ");
                sb.Append("We can move at whatever pace feels safe for you.'\n\n");
            }
            
            sb.Append("Original Text (read carefully for specific details):\n");
            sb.Append(originalText);
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
            
            if (request != null && (request.Mood.HasValue || request.Stress.HasValue || request.Purpose.HasValue))
            {
                sb.Append("Self-Reported Scores:\n");
                if (request.Mood.HasValue) sb.Append($"- Mood: {request.Mood.Value}/10\n");
                if (request.Stress.HasValue) sb.Append($"- Stress: {request.Stress.Value}/10\n");
                if (request.Purpose.HasValue) sb.Append($"- Purpose: {request.Purpose.Value}/10\n");
                sb.Append("\n");
            }
            
            sb.Append("CRITICAL RESPONSE REQUIREMENTS:\n");
            sb.Append("1. This is a CONVERSATION, not a summary. Write as if you're a therapist responding to them.\n");
            sb.Append("2. Include 1-2 reflective questions that invite them to explore deeper\n");
            sb.Append("3. Reference SPECIFIC details they mentioned (names, situations, feelings)\n");
            sb.Append("4. Validate their experience authentically\n");
            sb.Append("5. Use warm, empathetic language - similar to ChatGPT's empathetic responses\n");
            sb.Append("6. Length: 3-5 sentences + 1-2 questions (aim for 150-250 words)\n");
            sb.Append("7. Do NOT just summarize what they said - engage with it therapeutically\n");
            sb.Append("8. Do NOT provide generic encouragement - be specific to their situation\n");
            sb.Append("9. Do NOT list tasks - this is about emotional processing, not task management\n\n");
            
            sb.Append("GOOD EXAMPLE (CBT-style):\n");
            sb.Append("It sounds like you're having the thought that you're failing at everything and can't focus. ");
            sb.Append("Let's pause and examine that—what evidence supports this thought, and what evidence might challenge it? ");
            sb.Append("I notice you mentioned feeling behind on work deadlines. When you think about 'failing at everything,' ");
            sb.Append("are there areas where you're actually making progress, even if small? ");
            sb.Append("What would it feel like to acknowledge both the struggle and any steps you've taken, however small?\n\n");
            
            sb.Append("GOOD EXAMPLE (DBT-style):\n");
            sb.Append("That sounds really overwhelming. Anyone in your situation would feel drained trying to balance ");
            sb.Append("everything you've described. Let's slow down for a moment—can you name what emotion feels strongest ");
            sb.Append("right now? Is it the anxiety about deadlines, or maybe something deeper like fear of disappointing others? ");
            sb.Append("Sometimes naming the emotion helps us understand what we actually need in this moment.\n\n");
            
            sb.Append("GOOD EXAMPLE (Trauma-Informed):\n");
            sb.Append("Thank you for sharing this with me. You're not required to fix anything right now, and it's okay ");
            sb.Append("that things feel overwhelming. We can move at whatever pace feels safe for you. ");
            sb.Append("I'm curious—when you think about what you shared, what feels most important to you right now? ");
            sb.Append("There's no right answer, and you have full control over how we proceed.\n\n");
            
            sb.Append("BAD EXAMPLE (avoid this - too generic):\n");
            sb.Append("You seem stressed about work. Here are some tasks to help you manage your time better. ");
            sb.Append("Remember to stay positive and take breaks.\n\n");
            
            sb.Append("OUTPUT FORMAT:\n");
            sb.Append("Return ONLY a JSON object with two fields:\n");
            sb.Append("{\n");
            sb.Append("  \"therapeuticResponse\": \"Your full therapist-style response (3-5 sentences + 1-2 questions)\",\n");
            sb.Append("  \"therapeuticApproach\": \"").Append(approach).Append("\"\n");
            sb.Append("}\n\n");
            
            sb.Append("CRITICAL: Return ONLY the JSON object. No markdown. No explanations. Start with { and end with }. ");
            sb.Append("The therapeuticResponse field should start directly with your therapist-style response text. [/INST]");
            return sb.ToString();
        }

        // Helper method to determine which therapeutic approach to use
        private static string DetermineTherapeuticApproach(
            string originalText, 
            List<string> emotions, 
            List<string> themes, 
            string? preferredApproach)
        {
            // If user specified a preference, use it
            if (!string.IsNullOrWhiteSpace(preferredApproach))
            {
                var approach = preferredApproach.Trim().ToLowerInvariant();
                if (approach == "cbt" || approach == "dbt" || approach == "traumainformed" || approach == "trauma-informed")
                    return approach == "traumainformed" || approach == "trauma-informed" ? "TraumaInformed" : approach.ToUpperInvariant();
            }
            
            var textLower = originalText.ToLowerInvariant();
            var emotionsLower = emotions.Select(e => e.ToLowerInvariant()).ToList();
            var themesLower = themes.Select(t => t.ToLowerInvariant()).ToList();
            
            // Trauma indicators: mentions of trauma, abuse, safety concerns, feeling unsafe
            var traumaKeywords = new[] { "trauma", "abuse", "unsafe", "triggered", "triggering", "violence", "assault", "victim", "survivor", "ptsd", "panic", "flashback" };
            if (traumaKeywords.Any(k => textLower.Contains(k)) || 
                emotionsLower.Any(e => traumaKeywords.Any(k => e.Contains(k))))
            {
                return "TraumaInformed";
            }
            
            // DBT indicators: strong emotions, emotional dysregulation, relationship issues, self-harm thoughts
            var dbtKeywords = new[] { "overwhelmed", "dysregulated", "can't control", "emotion", "feeling", "relationship", "conflict", "borderline", "self-harm", "suicidal" };
            var strongEmotions = new[] { "angry", "rage", "furious", "desperate", "hopeless", "empty", "numb" };
            if (dbtKeywords.Any(k => textLower.Contains(k)) || 
                strongEmotions.Any(e => emotionsLower.Any(em => em.Contains(e))) ||
                themesLower.Any(t => t.Contains("relationship") || t.Contains("emotion")))
            {
                return "DBT";
            }
            
            // CBT indicators: negative thoughts, cognitive distortions, anxiety, depression, work stress
            var cbtKeywords = new[] { "thinking", "thought", "believe", "worry", "anxious", "depressed", "failure", "worthless", "should", "must", "always", "never" };
            if (cbtKeywords.Any(k => textLower.Contains(k)) ||
                themesLower.Any(t => t.Contains("work") || t.Contains("stress") || t.Contains("anxiety")))
            {
                return "CBT";
            }
            
            // Default to adaptive (mix of approaches)
            return "Adaptive";
        }

        // Parser for Step 7: Therapeutic Response
        public static (string? TherapeuticResponse, string? TherapeuticApproach) ParseTherapeuticResponse(string aiResponse, ILogger? logger = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    logger?.LogWarning("Therapeutic response is null or empty");
                    return (null, null);
                }
                
                // Extract text from RunPod response envelope
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
                logger?.LogDebug("Extracted therapeutic response text: {Text}", extractedText?.Substring(0, Math.Min(300, extractedText?.Length ?? 0)));
                
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
                    logger?.LogWarning("No JSON object found in therapeutic response");
                    return (null, null);
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
                
                var therapeuticResponse = root.TryGetProperty("therapeuticResponse", out var responseEl) 
                    ? responseEl.GetString() 
                    : null;
                
                var therapeuticApproach = root.TryGetProperty("therapeuticApproach", out var approachEl) 
                    ? approachEl.GetString() 
                    : null;
                
                logger?.LogDebug("Parsed therapeutic response: Approach={Approach}, HasResponse={HasResponse}", 
                    therapeuticApproach, 
                    !string.IsNullOrWhiteSpace(therapeuticResponse));
                
                return (therapeuticResponse, therapeuticApproach);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse therapeutic response");
                return (null, null);
            }
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
                
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
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
                
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
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
                // Extract text from RunPod response envelope (handles both new and old structures)
                var extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
                var cleanText = CleanJsonText(extractedText, logger);
                
                // Ensure we extract only the JSON array part (handles cases where AI adds explanatory text)
                var jsonStart = cleanText.IndexOf('[');
                var jsonEnd = cleanText.LastIndexOf(']');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    cleanText = cleanText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    logger?.LogDebug("Extracted JSON array from response (length: {Length})", cleanText.Length);
                }
                else
                {
                    logger?.LogWarning("No JSON array found in response. Text: {Text}", cleanText.Substring(0, Math.Min(200, cleanText.Length)));
                    return new List<TaskSuggestion>();
                }
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var tasks = JsonSerializer.Deserialize<List<TaskSuggestion>>(cleanText, options);
                
                return tasks ?? new List<TaskSuggestion>();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse task suggestions response: {Error}", ex.Message);
                return new List<TaskSuggestion>();
            }
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

				// Step 1: Parse the RunPod envelope and extract text (handles both new and old structures)
				// The incoming aiResponse is the raw JSON returned by RunPod.
				string extractedText = aiResponse;
				try
				{
					extractedText = RunpodResponseHelper.ExtractTextFromRunpodResponse(aiResponse);
					
					// If extraction failed, fallback to raw response
					if (string.IsNullOrWhiteSpace(extractedText) || extractedText == aiResponse)
					{
						extractedText = aiResponse;
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
				
				// Extract text from RunPod response envelope if present (handles both new and old structures)
				try
				{
					var extracted = RunpodResponseHelper.ExtractTextFromRunpodResponse(cleanResponse);
					if (!string.IsNullOrWhiteSpace(extracted) && extracted != cleanResponse)
					{
						cleanResponse = extracted;
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
				
				// Extract text from RunPod response envelope if present (handles both new and old structures)
				try
				{
					var extracted = RunpodResponseHelper.ExtractTextFromRunpodResponse(cleanResponse);
					if (!string.IsNullOrWhiteSpace(extracted) && extracted != cleanResponse)
					{
						cleanResponse = extracted;
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



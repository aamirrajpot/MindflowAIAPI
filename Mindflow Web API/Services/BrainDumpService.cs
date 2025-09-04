using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Utilities;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Services
{
	public interface IBrainDumpService
	{
		Task<List<TaskSuggestion>> GetTaskSuggestionsAsync(BrainDumpRequest request, int maxTokens = 1000, double temperature = 0.7);
	}

	public class BrainDumpService : IBrainDumpService
	{
		private readonly IRunPodService _runPodService;
		private readonly MindflowDbContext _db;
		private readonly ILogger<BrainDumpService> _logger;

		public BrainDumpService(IRunPodService runPodService, ILogger<BrainDumpService> logger, MindflowDbContext db)
		{
			_runPodService = runPodService;
			_logger = logger;
			_db = db;
		}

		public async Task<List<TaskSuggestion>> GetTaskSuggestionsAsync(BrainDumpRequest request, int maxTokens = 1200, double temperature = 0.7)
		{
			var prompt = BrainDumpPromptBuilder.BuildTaskSuggestionsPrompt(request);

			// Create entry
			var entry = new BrainDumpEntry
			{
				UserId = Guid.Empty, // set by endpoint using auth; keeps service pure if needed
				Text = request.Text,
				Context = request.Context,
				Mood = request.Mood,
				Stress = request.Stress,
				Purpose = request.Purpose,
				TokensEstimate = request.Text?.Length,
				CreatedAtUtc = DateTime.UtcNow
			};
			_db.BrainDumpEntries.Add(entry);
			await _db.SaveChangesAsync();

			var response = await _runPodService.SendPromptAsync(prompt, maxTokens, temperature);

			// Reuse existing tolerant parser (JSON array or numbered list)
			var tasks = LlamaPromptBuilderForRunpod.ParseTaskSuggestions(response);

			// Save preview
			entry.SuggestionsPreview = tasks != null && tasks.Count > 0
				? string.Join("; ", tasks.Take(3).Select(t => t.Task))
				: null;
			await _db.SaveChangesAsync();

			return tasks;
		}
	}
}



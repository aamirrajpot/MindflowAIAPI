
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Services
{
    public interface IOllamaService 
    {
        Task<AiAnalysisResult> Analyze(string journalText);
        Task<WellnessAnalysisResult> AnalyzeWellnessAsync(WellnessCheckIn checkIn);
        Task<List<string>> SuggestTasksAsync(WellnessCheckIn checkIn);
        Task<UrgencyAssessment> AssessUrgencyAsync(WellnessCheckIn checkIn);
        Task<ComprehensiveWellnessAnalysis> AnalyzeComprehensiveAsync(WellnessCheckIn checkIn);
    }
}
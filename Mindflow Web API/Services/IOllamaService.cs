
using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IOllamaService 
    {
        Task<AiAnalysisResult> Analyze(string journalText);
    }
}
using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    public class WellnessSnapshotService : IWellnessSnapshotService
    {
        private readonly MindflowDbContext _context;

        public WellnessSnapshotService(MindflowDbContext context)
        {
            _context = context;
        }

        public async Task<WellnessSnapshotDto> GetWellnessSnapshotAsync(Guid userId, int days = 7)
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-days + 1);
            
            return await GetWellnessSnapshotForPeriodAsync(userId, startDate, endDate);
        }

        public async Task<WellnessSnapshotDto> GetWellnessSnapshotForPeriodAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            // Get journal entries for the specified period
            var entries = await _context.BrainDumpEntries
                .Where(e => e.UserId == userId 
                    && e.CreatedAtUtc.Date >= startDate 
                    && e.CreatedAtUtc.Date <= endDate
                    && e.DeletedAtUtc == null)
                .OrderBy(e => e.CreatedAtUtc)
                .ToListAsync();

            // Group entries by date and calculate daily averages
            var dataPoints = new List<WellnessDataPointDto>();
            
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayEntries = entries.Where(e => e.CreatedAtUtc.Date == date).ToList();
                
                var moodValues = dayEntries.Where(e => e.Mood.HasValue).Select(e => (double)e.Mood.Value).ToList();
                var stressValues = dayEntries.Where(e => e.Stress.HasValue).Select(e => (double)e.Stress.Value).ToList();
                
                // Calculate energy as inverse of stress (assuming stress scale is 1-10, higher = more stress)
                // Energy = 10 - stress, but we'll use mood as energy proxy for now
                var energyValues = moodValues.ToList(); // Using mood as energy proxy
                
                var dataPoint = new WellnessDataPointDto(
                    Date: date,
                    DayOfWeek: date.ToString("ddd"),
                    Mood: moodValues.Any() ? moodValues.Average() : (double?)null,
                    Energy: energyValues.Any() ? energyValues.Average() : (double?)null,
                    Stress: stressValues.Any() ? stressValues.Average() : (double?)null,
                    EntryCount: dayEntries.Count
                );
                
                dataPoints.Add(dataPoint);
            }

            // Calculate trends
            var trends = CalculateTrends(dataPoints);

            // Generate insights
            var insights = GenerateInsights(dataPoints, trends);

            return new WellnessSnapshotDto(dataPoints, trends, insights);
        }

        private WellnessTrendsDto CalculateTrends(List<WellnessDataPointDto> dataPoints)
        {
            var validMoodPoints = dataPoints.Where(p => p.Mood.HasValue).ToList();
            var validEnergyPoints = dataPoints.Where(p => p.Energy.HasValue).ToList();
            var validStressPoints = dataPoints.Where(p => p.Stress.HasValue).ToList();

            var moodTrend = CalculateTrend(validMoodPoints.Select(p => p.Mood!.Value).ToList());
            var energyTrend = CalculateTrend(validEnergyPoints.Select(p => p.Energy!.Value).ToList());
            var stressTrend = CalculateTrend(validStressPoints.Select(p => p.Stress!.Value).ToList());

            var moodChangePercentage = CalculateChangePercentage(validMoodPoints.Select(p => p.Mood!.Value).ToList());
            var energyChangePercentage = CalculateChangePercentage(validEnergyPoints.Select(p => p.Energy!.Value).ToList());
            var stressChangePercentage = CalculateChangePercentage(validStressPoints.Select(p => p.Stress!.Value).ToList());

            return new WellnessTrendsDto(
                MoodTrend: moodTrend,
                EnergyTrend: energyTrend,
                StressTrend: stressTrend,
                MoodChangePercentage: moodChangePercentage,
                EnergyChangePercentage: energyChangePercentage,
                StressChangePercentage: stressChangePercentage
            );
        }

        private string CalculateTrend(List<double> values)
        {
            if (values.Count < 2) return "stable";

            var firstHalf = values.Take(values.Count / 2).Average();
            var secondHalf = values.Skip(values.Count / 2).Average();

            var change = secondHalf - firstHalf;
            var threshold = 0.5; // Minimum change to consider significant

            if (change > threshold) return "improving";
            if (change < -threshold) return "declining";
            return "stable";
        }

        private double CalculateChangePercentage(List<double> values)
        {
            if (values.Count < 2) return 0;

            var firstValue = values.First();
            var lastValue = values.Last();

            if (firstValue == 0) return 0;

            return ((lastValue - firstValue) / firstValue) * 100;
        }

        private WellnessInsightsDto GenerateInsights(List<WellnessDataPointDto> dataPoints, WellnessTrendsDto trends)
        {
            var moodInsights = new List<string>();
            var energyInsights = new List<string>();
            var stressInsights = new List<string>();
            var recommendations = new List<string>();

            // Mood insights
            var avgMood = dataPoints.Where(p => p.Mood.HasValue).Average(p => p.Mood!.Value);
            if (avgMood < 3)
                moodInsights.Add("Your mood has been consistently low. Consider reaching out for support.");
            else if (avgMood > 7)
                moodInsights.Add("You've been in great spirits! Keep up the positive momentum.");
            else
                moodInsights.Add("Your mood has been relatively stable with room for improvement.");

            // Energy insights
            var avgEnergy = dataPoints.Where(p => p.Energy.HasValue).Average(p => p.Energy!.Value);
            if (avgEnergy < 3)
                energyInsights.Add("Your energy levels have been low. Consider reviewing your sleep and nutrition.");
            else if (avgEnergy > 7)
                energyInsights.Add("You've been feeling energetic! Maintain your current routine.");
            else
                energyInsights.Add("Your energy levels are moderate. Small lifestyle adjustments could help boost them.");

            // Stress insights
            var avgStress = dataPoints.Where(p => p.Stress.HasValue).Average(p => p.Stress!.Value);
            if (avgStress > 7)
                stressInsights.Add("You've been experiencing high stress levels. Consider stress management techniques.");
            else if (avgStress < 3)
                stressInsights.Add("You've been managing stress well. Keep up the good work!");
            else
                stressInsights.Add("Your stress levels are moderate. Continue monitoring and managing as needed.");

            // Recommendations based on trends
            if (trends.MoodTrend == "declining")
                recommendations.Add("Consider journaling about positive experiences or practicing gratitude.");
            if (trends.EnergyTrend == "declining")
                recommendations.Add("Review your sleep schedule and consider adding light exercise to your routine.");
            if (trends.StressTrend == "improving")
                recommendations.Add("Great job managing stress! Continue with your current strategies.");
            else if (trends.StressTrend == "declining")
                recommendations.Add("Try deep breathing exercises or meditation to help manage stress.");

            // General recommendations
            var totalEntries = dataPoints.Sum(p => p.EntryCount);
            if (totalEntries < 3)
                recommendations.Add("Consider journaling more frequently to better track your wellness patterns.");
            else if (totalEntries > 10)
                recommendations.Add("You're doing great with consistent journaling! This helps track your wellness journey.");

            return new WellnessInsightsDto(moodInsights, energyInsights, stressInsights, recommendations);
        }
    }
}

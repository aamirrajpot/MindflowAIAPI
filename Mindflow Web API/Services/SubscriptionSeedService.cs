using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mindflow_Web_API.Services
{
    public class SubscriptionSeedService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<SubscriptionSeedService> _logger;

        public SubscriptionSeedService(MindflowDbContext dbContext, ILogger<SubscriptionSeedService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task SeedSubscriptionDataAsync()
        {
            try
            {
                // Check if data already exists
                if (await _dbContext.SubscriptionPlans.AnyAsync())
                {
                    _logger.LogInformation("Subscription data already seeded. Skipping...");
                    return;
                }

                _logger.LogInformation("Seeding subscription data...");

                // Create subscription features based on the screenshots
                var features = new List<SubscriptionFeature>
                {
                    new() { Name = "Basic journaling", Description = "Basic journaling capabilities", SortOrder = 1, Icon = "journal" },
                    new() { Name = "Simple AI insights", Description = "Basic AI insights and analysis", SortOrder = 2, Icon = "ai" },
                    new() { Name = "3 wellness check-ins per week", Description = "Limited wellness check-ins", SortOrder = 3, Icon = "wellness" },
                    new() { Name = "Basic task suggestions", Description = "Basic task recommendations", SortOrder = 4, Icon = "tasks" },
                    new() { Name = "Unlimited journaling", Description = "Unlimited journaling capabilities", SortOrder = 5, Icon = "journal-unlimited" },
                    new() { Name = "Advanced AI insights & analysis", Description = "Advanced AI insights and detailed analysis", SortOrder = 6, Icon = "ai-advanced" },
                    new() { Name = "Unlimited wellness check-ins", Description = "Unlimited wellness check-ins", SortOrder = 7, Icon = "wellness-unlimited" },
                    new() { Name = "Personalized task recommendations", Description = "Personalized task recommendations", SortOrder = 8, Icon = "tasks-personalized" },
                    new() { Name = "Calendar integration", Description = "Calendar integration features", SortOrder = 9, Icon = "calendar" },
                    new() { Name = "Weekly wellness reports", Description = "Weekly wellness reports and analytics", SortOrder = 10, Icon = "reports" },
                    new() { Name = "Priority support", Description = "Priority customer support", SortOrder = 11, Icon = "support" },
                    new() { Name = "Export your data", Description = "Data export capabilities", SortOrder = 12, Icon = "export" },
                    new() { Name = "Advanced mood tracking", Description = "Advanced mood tracking features", SortOrder = 13, Icon = "mood" },
                    new() { Name = "Custom reminder schedules", Description = "Custom reminder scheduling", SortOrder = 14, Icon = "reminders" }
                };

                await _dbContext.SubscriptionFeatures.AddRangeAsync(features);
                await _dbContext.SaveChangesAsync();

                // Create subscription plans
                var freePlan = new SubscriptionPlan
                {
                    Name = "Free",
                    Description = "Basic wellness features",
                    Price = 0,
                    BillingCycle = "Forever",
                    IsActive = true,
                    SortOrder = 1,
                    IsPopular = false
                };

                var premiumPlan = new SubscriptionPlan
                {
                    Name = "Premium Monthly",
                    Description = "Advanced wellness features with unlimited access",
                    Price = 9.99m,
                    BillingCycle = "Monthly",
                    IsActive = true,
                    SortOrder = 2,
                    OriginalPrice = "19.99",
                    IsPopular = true
                };

                await _dbContext.SubscriptionPlans.AddRangeAsync(freePlan, premiumPlan);
                await _dbContext.SaveChangesAsync();

                // Create plan-feature relationships
                var planFeatures = new List<PlanFeature>();

                // Free plan features (limited)
                var freePlanFeatures = features.Where(f => f.Name.Contains("Basic") || f.Name.Contains("3 wellness")).ToList();
                foreach (var feature in freePlanFeatures)
                {
                    planFeatures.Add(new PlanFeature
                    {
                        PlanId = freePlan.Id,
                        FeatureId = feature.Id,
                        IsIncluded = true,
                        Limit = feature.Name.Contains("3 wellness") ? "3 per week" : null
                    });
                }

                // Premium plan features (all features)
                foreach (var feature in features)
                {
                    planFeatures.Add(new PlanFeature
                    {
                        PlanId = premiumPlan.Id,
                        FeatureId = feature.Id,
                        IsIncluded = true,
                        Limit = feature.Name.Contains("Unlimited") ? "Unlimited" : null
                    });
                }

                await _dbContext.PlanFeatures.AddRangeAsync(planFeatures);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Subscription data seeded successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding subscription data");
                throw;
            }
        }
    }
}

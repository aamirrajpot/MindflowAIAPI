
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
        private readonly IConfiguration _configuration;


        public SubscriptionSeedService(MindflowDbContext dbContext, ILogger<SubscriptionSeedService> logger,IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;

        }
        public async Task SeedSubscriptionDataAsync(CancellationToken cancellationToken = default)
        {
            await SeedStoreProductsAsync(cancellationToken);
        }

        private async Task SeedStoreProductsAsync(CancellationToken ct)
        {
            var section = _configuration.GetSection("StoreProducts");
            if (!section.Exists())
            {
                _logger.LogInformation("No StoreProducts configuration found; skipping StoreProduct seeding.");
                return;
            }

            await SeedProviderAsync(SubscriptionProvider.Apple, section.GetSection("Apple"), ct);
            await SeedProviderAsync(SubscriptionProvider.Google, section.GetSection("Google"), ct);
        }

        private async Task SeedProviderAsync(SubscriptionProvider provider, IConfigurationSection providerSection, CancellationToken ct)
        {
            if (!providerSection.Exists()) return;

            foreach (var envSection in providerSection.GetChildren()) // e.g., production, sandbox
            {
                var environment = envSection.Key;
                var items = envSection.Get<List<StoreProductSeedItem>>() ?? new();
                foreach (var item in items)
                {
                    var plan = await _dbContext.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Name == item.PlanName, ct);
                    if (plan == null)
                    {
                        _logger.LogWarning("StoreProduct seed skipped: plan '{PlanName}' not found for {Provider} {Environment} {ProductId}", item.PlanName, provider, environment, item.ProductId);
                        continue;
                    }

                    var existing = await _dbContext.Set<StoreProduct>()
                        .FirstOrDefaultAsync(sp => sp.Provider == provider && sp.Environment == environment && sp.ProductId == item.ProductId, ct);

                    if (existing == null)
                    {
                        await _dbContext.Set<StoreProduct>().AddAsync(new StoreProduct
                        {
                            Provider = provider,
                            Environment = environment,
                            ProductId = item.ProductId,
                            PlanId = plan.Id
                        }, ct);
                        _logger.LogInformation("Seeded StoreProduct {Provider} {Environment} {ProductId} -> plan {PlanName}", provider, environment, item.ProductId, item.PlanName);
                    }
                    else if (existing.PlanId != plan.Id)
                    {
                        existing.PlanId = plan.Id;
                        _logger.LogInformation("Updated StoreProduct {Provider} {Environment} {ProductId} -> plan {PlanName}", provider, environment, item.ProductId, item.PlanName);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        private record StoreProductSeedItem(string ProductId, string PlanName);
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

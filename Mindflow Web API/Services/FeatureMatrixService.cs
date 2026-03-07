using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Resolves subscription tier from UserSubscription.PlanId (plan name: weekly, monthly, annual)
    /// and applies the feature matrix for gating and limits.
    /// </summary>
    public class FeatureMatrixService : IFeatureMatrixService
    {
        private readonly MindflowDbContext _db;
        private readonly ILogger<FeatureMatrixService> _logger;

        public FeatureMatrixService(MindflowDbContext db, ILogger<FeatureMatrixService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<SubscriptionTier> GetTierAsync(Guid userId)
        {
            var sub = await _db.UserSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(us =>
                    us.UserId == userId &&
                    (us.Status == SubscriptionStatus.Active || us.Status == SubscriptionStatus.InGrace));

            if (sub == null)
                return SubscriptionTier.Free;

            var now = DateTime.UtcNow;
            var expiry = sub.EndDate ?? sub.ExpiresAtUtc;
            if (expiry.HasValue && expiry.Value < now)
                return SubscriptionTier.Free;

            return await ResolveTierFromPlanIdAsync(sub.PlanId, sub.Provider);
        }

        /// <summary>
        /// Resolves tier from PlanId string. Tries StoreProduct → SubscriptionPlan.Name first,
        /// then falls back to parsing PlanId for "annual", "monthly", "weekly" (case-insensitive).
        /// </summary>
        private async Task<SubscriptionTier> ResolveTierFromPlanIdAsync(string planId, SubscriptionProvider provider)
        {
            var storeProduct = await _db.StoreProducts
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.ProductId == planId && sp.Provider == provider);

            if (storeProduct != null)
            {
                var plan = await _db.SubscriptionPlans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == storeProduct.PlanId);
                if (plan != null && !string.IsNullOrWhiteSpace(plan.Name))
                {
                    var tier = ParseTierFromPlanName(plan.Name);
                    if (tier != SubscriptionTier.Free)
                        return tier;
                }
            }

            return ResolveTierFromPlanId(planId, provider);
        }

        private SubscriptionTier ResolveTierFromPlanId(string planId, SubscriptionProvider provider)
        {
            if (string.IsNullOrWhiteSpace(planId))
                return SubscriptionTier.Free;

            var normalized = planId.Trim().ToLowerInvariant();
            if (normalized.Contains("annual"))
                return SubscriptionTier.Annual;
            if (normalized.Contains("monthly"))
                return SubscriptionTier.Monthly;
            if (normalized.Contains("weekly"))
                return SubscriptionTier.Weekly;

            _logger.LogDebug("PlanId '{PlanId}' did not match weekly/monthly/annual; defaulting to Weekly for paid subscription.", planId);
            return SubscriptionTier.Weekly;
        }

        private static SubscriptionTier ParseTierFromPlanName(string planName)
        {
            var name = planName.Trim().ToLowerInvariant();
            if (name.Contains("annual"))
                return SubscriptionTier.Annual;
            if (name.Contains("monthly"))
                return SubscriptionTier.Monthly;
            if (name.Contains("weekly"))
                return SubscriptionTier.Weekly;
            return SubscriptionTier.Free;
        }

        public async Task<bool> CanUseFeatureAsync(Guid userId, string featureKey)
        {
            var tier = await GetTierAsync(userId);
            return featureKey switch
            {
                SubscriptionFeatures.BrainDump => FeatureMatrix.HasBrainDump(tier),
                SubscriptionFeatures.CalendarAutoSchedule => FeatureMatrix.HasCalendarAutoSchedule(tier),
                SubscriptionFeatures.AdvancedInsights => FeatureMatrix.HasAdvancedInsights(tier),
                SubscriptionFeatures.DataExport => FeatureMatrix.HasDataExport(tier),
                SubscriptionFeatures.WeeklyReflection => FeatureMatrix.HasWeeklyReflection(tier),
                SubscriptionFeatures.SmartPrioritization => FeatureMatrix.HasSmartPrioritization(tier),
                _ => false
            };
        }

        public async Task<int?> GetBrainDumpWeeklyLimitAsync(Guid userId)
        {
            var tier = await GetTierAsync(userId);
            return FeatureMatrix.BrainDumpWeeklyLimit(tier);
        }

        public async Task<int> GetBrainDumpCountThisWeekAsync(Guid userId)
        {
            var (start, end) = GetUtcWeekBounds();
            return await _db.BrainDumpEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId && e.CreatedAtUtc >= start && e.CreatedAtUtc < end && e.DeletedAtUtc == null);
        }

        public async Task<bool> CanPerformBrainDumpAsync(Guid userId)
        {
            var limit = await GetBrainDumpWeeklyLimitAsync(userId);
            if (limit == null)
                return true;
            var count = await GetBrainDumpCountThisWeekAsync(userId);
            return count < limit.Value;
        }

        public async Task<bool> CanUseCalendarAutoScheduleAsync(Guid userId)
        {
            var tier = await GetTierAsync(userId);
            return FeatureMatrix.HasCalendarAutoSchedule(tier);
        }

        private static (DateTime start, DateTime end) GetUtcWeekBounds()
        {
            var now = DateTime.UtcNow;
            var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);
            return (startOfWeek, endOfWeek);
        }
    }
}

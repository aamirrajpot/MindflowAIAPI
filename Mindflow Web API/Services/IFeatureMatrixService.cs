using System;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Resolves user's subscription tier from PlanId (plan name: weekly, monthly, annual)
    /// and answers feature access and limits per the subscription feature matrix.
    /// </summary>
    public interface IFeatureMatrixService
    {
        /// <summary>Gets the subscription tier for the user (Free if no active subscription).</summary>
        Task<SubscriptionTier> GetTierAsync(Guid userId);

        /// <summary>Returns true if the user's tier has access to the feature.</summary>
        Task<bool> CanUseFeatureAsync(Guid userId, string featureKey);

        /// <summary>Max brain dumps per week for the user's tier; null = unlimited.</summary>
        Task<int?> GetBrainDumpWeeklyLimitAsync(Guid userId);

        /// <summary>Count of brain dumps the user has already used in the current week (UTC).</summary>
        Task<int> GetBrainDumpCountThisWeekAsync(Guid userId);

        /// <summary>Returns true if the user can perform another brain dump this week (under limit or unlimited).</summary>
        Task<bool> CanPerformBrainDumpAsync(Guid userId);

        /// <summary>Returns true if the user can use calendar auto-scheduling (Weekly+).</summary>
        Task<bool> CanUseCalendarAutoScheduleAsync(Guid userId);
    }
}

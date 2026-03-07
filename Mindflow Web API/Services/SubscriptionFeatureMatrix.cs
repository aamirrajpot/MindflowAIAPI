namespace Mindflow_Web_API.Services
{
    /// <summary>
    /// Subscription tier derived from UserSubscription.PlanId (plan name: weekly, monthly, annual).
    /// Free = no active subscription.
    /// </summary>
    public enum SubscriptionTier
    {
        Free = 0,
        Weekly = 1,
        Monthly = 2,
        Annual = 3
    }

    /// <summary>
    /// Feature keys used for tier-based gating. Align with client tier structure:
    /// Free: limited brain dumps, no calendar, no insights, no export.
    /// Weekly+: full brain dump, calendar, basic priority.
    /// Monthly+: smart prioritization, calendar blocking, weekly reflection.
    /// Annual: advanced insights, priority support, data export.
    /// </summary>
    public static class SubscriptionFeatures
    {
        /// <summary>Brain dump suggestions (Free = limited per week, paid = full).</summary>
        public const string BrainDump = "BrainDump";

        /// <summary>Calendar auto-scheduling / add-to-calendar (Weekly+).</summary>
        public const string CalendarAutoSchedule = "CalendarAutoSchedule";

        /// <summary>Advanced insights / long-term pattern tracking (Monthly+).</summary>
        public const string AdvancedInsights = "AdvancedInsights";

        /// <summary>Data export (Annual or as configured).</summary>
        public const string DataExport = "DataExport";

        /// <summary>Basic weekly reflection summary (Monthly+).</summary>
        public const string WeeklyReflection = "WeeklyReflection";

        /// <summary>Smart prioritization / calendar blocking (Monthly+).</summary>
        public const string SmartPrioritization = "SmartPrioritization";
    }

    /// <summary>
    /// Feature matrix: which tier gets which feature. Used by FeatureMatrixService.
    /// </summary>
    public static class FeatureMatrix
    {
        /// <summary>Max brain dumps per week for Free tier. Null = unlimited.</summary>
        public static int? BrainDumpWeeklyLimit(SubscriptionTier tier)
        {
            return tier == SubscriptionTier.Free ? 3 : null;
        }

        public static bool HasCalendarAutoSchedule(SubscriptionTier tier) =>
            tier >= SubscriptionTier.Weekly;

        public static bool HasAdvancedInsights(SubscriptionTier tier) =>
            tier >= SubscriptionTier.Monthly;

        public static bool HasDataExport(SubscriptionTier tier) =>
            tier >= SubscriptionTier.Annual;

        public static bool HasWeeklyReflection(SubscriptionTier tier) =>
            tier >= SubscriptionTier.Monthly;

        public static bool HasSmartPrioritization(SubscriptionTier tier) =>
            tier >= SubscriptionTier.Monthly;

        public static bool HasBrainDump(SubscriptionTier tier) => true; // All tiers, but Free is rate-limited
    }
}

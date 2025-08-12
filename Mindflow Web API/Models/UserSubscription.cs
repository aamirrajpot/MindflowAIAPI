using System;

namespace Mindflow_Web_API.Models
{
    public class UserSubscription : EntityBase
    {
        public Guid UserId { get; set; }
        public Guid PlanId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; } // null for active subscriptions
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        
        // Navigation properties
        public User User { get; set; } = null!;
        public SubscriptionPlan Plan { get; set; } = null!;
    }

    public enum SubscriptionStatus
    {
        Active,
        Cancelled,
        Expired,
        Pending,
        PastDue
    }
}

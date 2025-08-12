using System;

namespace Mindflow_Web_API.Models
{
    public class PlanFeature : EntityBase
    {
        public Guid PlanId { get; set; }
        public Guid FeatureId { get; set; }
        public bool IsIncluded { get; set; } = true;
        public string? Limit { get; set; } // e.g., "3 per week", "Unlimited", etc.
        
        // Navigation properties
        public SubscriptionPlan Plan { get; set; } = null!;
        public SubscriptionFeature Feature { get; set; } = null!;
    }
}

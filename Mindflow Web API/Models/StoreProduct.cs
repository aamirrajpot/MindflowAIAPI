using System;

namespace Mindflow_Web_API.Models
{
    public class StoreProduct : EntityBase
    {
        public SubscriptionProvider Provider { get; set; }
        public string ProductId { get; set; } = string.Empty; // Store product identifier
        public Guid PlanId { get; set; }
        public string Environment { get; set; } = "production"; // or sandbox
    }
}



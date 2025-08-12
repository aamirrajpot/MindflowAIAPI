using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.Models
{
    public class SubscriptionPlan : EntityBase
    {
        public string Name { get; set; } = string.Empty; // "Free", "Premium Monthly"
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0; // 0 for free, 9.99 for premium
        public string BillingCycle { get; set; } = string.Empty; // "Forever", "Monthly", "Yearly"
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0; // For display order
        public string? OriginalPrice { get; set; } // For showing strikethrough prices
        public bool IsPopular { get; set; } = false; // To highlight recommended plan
        
        // Navigation properties
        public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
    }
}

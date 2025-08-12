using System;

namespace Mindflow_Web_API.Models
{
    public class SubscriptionFeature : EntityBase
    {
        public string Name { get; set; } = string.Empty; // "Unlimited journaling", "Advanced AI insights", etc.
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0; // For display order
        public string Icon { get; set; } = string.Empty; // Icon name or identifier
    }
}

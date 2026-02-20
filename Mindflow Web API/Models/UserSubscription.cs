using System;

namespace Mindflow_Web_API.Models
{
    public class UserSubscription : EntityBase
    {
        public Guid UserId { get; set; }
        public string PlanId { get; set; } = string.Empty; // Store product identifier (e.g., Apple productId or Google productId)
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; } // null for active subscriptions
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        
        // Provider-aware fields (Apple/Google)
        public SubscriptionProvider Provider { get; set; } = SubscriptionProvider.Apple;
        public string ProductId { get; set; } = string.Empty; // Store product identifier (same as PlanId for Apple/Google)
        public string OriginalTransactionId { get; set; } = string.Empty; // Apple originalTransactionId or Google purchaseToken
        public string LatestTransactionId { get; set; } = string.Empty; // Latest transaction id (or purchaseToken)
        public DateTime? ExpiresAtUtc { get; set; } // Authoritative expiry from store
        public bool AutoRenewEnabled { get; set; } = true;
        public string Environment { get; set; } = "production"; // or sandbox
        
        // Optional raw payloads for support/audit
        public string? RawNotificationPayload { get; set; } // Complete outer signedPayload from Apple webhook
        public string? RawRenewalPayload { get; set; }
        public string? RawTransactionPayload { get; set; }
        
        // Optional cross-link fields
        public Guid? AppAccountToken { get; set; } // Apple
        public string? GoogleObfuscatedAccountId { get; set; }
        public string? GoogleObfuscatedProfileId { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
    }

    public enum SubscriptionStatus
    {
        Active,
        Cancelled,
        Expired,
        Pending,
        PastDue,
        InGrace,
        BillingRetry
    }

    public enum SubscriptionProvider
    {
        Apple = 1,
        Google = 2
    }
}

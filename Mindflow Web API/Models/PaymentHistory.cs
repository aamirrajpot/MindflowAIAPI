using System;

namespace Mindflow_Web_API.Models
{
    public class PaymentHistory : EntityBase
    {
        public Guid UserId { get; set; }
        public Guid? PaymentCardId { get; set; } // Optional, in case payment was made without saved card
        public Guid? SubscriptionPlanId { get; set; } // What was purchased
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Description { get; set; } = string.Empty; // e.g., "Premium Monthly"
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public string? TransactionId { get; set; } // External payment provider transaction ID
        public string? PaymentMethod { get; set; } // "Card", "Apple Pay", "Google Pay", etc.
        public string? FailureReason { get; set; } // If payment failed
        public DateTime TransactionDate { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
        public PaymentCard? PaymentCard { get; set; }
        public SubscriptionPlan? SubscriptionPlan { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,
        Success,
        Failed,
        Cancelled,
        Refunded
    }
}

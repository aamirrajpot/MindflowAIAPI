using Mindflow_Web_API.Models;
using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.DTOs
{
    /// <summary>
    /// Apple subscribe: either JWS (StoreKit 2) or legacy transaction receipt.
    /// - JWS path: provide SignedTransactionJws, OriginalTransactionId, ExpiresAtUtc (and optionally SignedRenewalInfoJws, AppAccountToken, Environment).
    /// - Legacy path: provide TransactionReceipt (base64) and TransactionDateMs; we verify with Apple and extract the rest.
    /// </summary>
    public record AppleSubscribeRequest(
        string ProductId,
        string TransactionId,
        // JWS path (StoreKit 2)
        string? SignedTransactionJws,
        string? OriginalTransactionId,
        DateTime? ExpiresAtUtc,
        string? SignedRenewalInfoJws,
        Guid? AppAccountToken,
        string? Environment,
        // Legacy receipt path (StoreKit 1 / transactionReceipt)
        string? TransactionReceipt,
        long? TransactionDateMs
    );

    public record AppleRestoreRequest(
        string ProductId,
        DateTime? ExpiresAtUtc,
        string OriginalTransactionId,
        Guid? AppAccountToken,
        string? Environment
    );

    public record AppleNotificationDto(
        string SignedPayload
    );
    // Subscription Plan DTOs
    public record SubscriptionPlanDto(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string BillingCycle,
        bool IsActive,
        int SortOrder,
        string? OriginalPrice,
        bool IsPopular,
        List<SubscriptionFeatureDto> Features
    );

    public record CreateSubscriptionPlanDto(
        string Name,
        string Description,
        decimal Price,
        string BillingCycle,
        bool IsActive = true,
        int SortOrder = 0,
        string? OriginalPrice = null,
        bool IsPopular = false
    );

    public record UpdateSubscriptionPlanDto(
        string? Name,
        string? Description,
        decimal? Price,
        string? BillingCycle,
        bool? IsActive,
        int? SortOrder,
        string? OriginalPrice,
        bool? IsPopular
    );

    // Subscription Feature DTOs
    public record SubscriptionFeatureDto(
        Guid Id,
        string Name,
        string Description,
        bool IsActive,
        int SortOrder,
        string Icon
    );

    public record CreateSubscriptionFeatureDto(
        string Name,
        string Description,
        bool IsActive = true,
        int SortOrder = 0,
        string Icon = ""
    );

    public record UpdateSubscriptionFeatureDto(
        string? Name,
        string? Description,
        bool? IsActive,
        int? SortOrder,
        string? Icon
    );

    // Plan Feature DTOs
    public record PlanFeatureDto(
        Guid Id,
        Guid PlanId,
        Guid FeatureId,
        bool IsIncluded,
        string? Limit,
        SubscriptionFeatureDto Feature
    );

    public record CreatePlanFeatureDto(
        Guid PlanId,
        Guid FeatureId,
        bool IsIncluded = true,
        string? Limit = null
    );

    public record UpdatePlanFeatureDto(
        bool? IsIncluded,
        string? Limit
    );

    // User Subscription DTOs
    public record UserSubscriptionDto(
        Guid Id,
        Guid UserId,
        string PlanId, // Store product identifier (e.g., Apple productId)
        DateTime StartDate,
        DateTime? EndDate,
        SubscriptionStatus Status,
        SubscriptionPlanDto? Plan, // Optional, may be null if PlanId is just a productId string
        // Additional subscription details
        SubscriptionProvider Provider, // Apple or Google
        string ProductId, // Same as PlanId, but explicit for clarity
        DateTime? ExpiresAtUtc, // Authoritative expiry date from store
        bool AutoRenewEnabled, // Whether auto-renewal is enabled
        string Environment // "production" or "sandbox"
    );

    public record CreateUserSubscriptionDto(
        string PlanId // Store product identifier
    );

    public record UpdateUserSubscriptionDto(
        string? PlanId,
        DateTime? EndDate,
        SubscriptionStatus? Status
    );

    // Subscription Management DTOs
    public record SubscriptionOverviewDto(
        UserSubscriptionDto? CurrentSubscription,
        List<SubscriptionPlanDto> AvailablePlans,
        List<SubscriptionFeatureDto> AllFeatures
    );

    public record CancelSubscriptionDto(
        string Reason = ""
    );

    // ── RevenueCat Webhook DTOs ──────────────────────────────────────────

    /// <summary>
    /// Root webhook payload sent by RevenueCat.
    /// Docs: https://www.revenuecat.com/docs/integrations/webhooks
    /// </summary>
    public class RevenueCatWebhookDto
    {
        public RevenueCatEventDto Event { get; set; } = null!;
        public string Api_version { get; set; } = "1.0";
    }

    public class RevenueCatEventDto
    {
        public string Type { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public long? Event_timestamp_ms { get; set; }
        public string? App_id { get; set; }

        // Transaction identifiers
        public string? Product_id { get; set; }
        public string? Transaction_id { get; set; }
        public string? Original_transaction_id { get; set; }

        // Timing
        public long? Purchased_at_ms { get; set; }
        public long? Expiration_at_ms { get; set; }
        public string? Period_type { get; set; }

        // Store / environment
        public string? Store { get; set; }
        public string? Environment { get; set; }
        public string? Country_code { get; set; }

        // User identification
        public string? App_user_id { get; set; }
        public string? Original_app_user_id { get; set; }
        public List<string>? Aliases { get; set; }

        // Entitlements
        public string? Entitlement_id { get; set; }
        public List<string>? Entitlement_ids { get; set; }
        public string? Presented_offering_id { get; set; }

        // Pricing
        public string? Currency { get; set; }
        public decimal? Price { get; set; }
        public decimal? Price_in_purchased_currency { get; set; }
        public double? Takehome_percentage { get; set; }
        public double? Tax_percentage { get; set; }
        public double? Commission_percentage { get; set; }

        // Type-specific fields
        public string? Cancel_reason { get; set; }
        public string? Expiration_reason { get; set; }
        public string? New_product_id { get; set; }
        public bool? Is_trial_conversion { get; set; }
        public bool? Is_family_share { get; set; }
        public long? Auto_resume_at_ms { get; set; }
        public List<string>? Transferred_from { get; set; }
        public List<string>? Transferred_to { get; set; }
        public string? Offer_code { get; set; }
        public int? Renewal_number { get; set; }

        public Dictionary<string, RevenueCatSubscriberAttribute>? Subscriber_attributes { get; set; }
    }

    public class RevenueCatSubscriberAttribute
    {
        public string? Value { get; set; }
        public long? Updated_at_ms { get; set; }
    }
}

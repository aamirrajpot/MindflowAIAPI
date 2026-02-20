using Mindflow_Web_API.Models;
using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.DTOs
{
     public record AppleSubscribeRequest(
        string ProductId,
        DateTime? ExpiresAtUtc,
        string SignedTransactionJws,
        string? SignedRenewalInfoJws,
        string TransactionId,
        string OriginalTransactionId,
        Guid? AppAccountToken,
        string? Environment
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
}

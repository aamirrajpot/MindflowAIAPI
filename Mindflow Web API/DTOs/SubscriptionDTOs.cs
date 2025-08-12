using Mindflow_Web_API.Models;
using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.DTOs
{
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
        Guid PlanId,
        DateTime StartDate,
        DateTime? EndDate,
        SubscriptionStatus Status,
        SubscriptionPlanDto Plan
    );

    public record CreateUserSubscriptionDto(
        Guid PlanId
    );

    public record UpdateUserSubscriptionDto(
        Guid? PlanId,
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

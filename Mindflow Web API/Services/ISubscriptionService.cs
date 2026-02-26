using Mindflow_Web_API.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public interface ISubscriptionService
    {
         // Provider-oriented
        Task<UserSubscriptionDto> ActivateAppleSubscriptionAsync(Guid userId, AppleSubscribeRequest dto);
        Task<UserSubscriptionDto?> RestoreAppleSubscriptionAsync(Guid userId, AppleRestoreRequest dto);
        Task<bool> ApplyAppleNotificationAsync(AppleNotificationDto notification);

        // Subscription Plans
        Task<SubscriptionPlanDto> CreatePlanAsync(CreateSubscriptionPlanDto dto);
        Task<SubscriptionPlanDto?> GetPlanByIdAsync(Guid planId);
        Task<IEnumerable<SubscriptionPlanDto>> GetAllPlansAsync();
        Task<SubscriptionPlanDto?> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanDto dto);
        Task<bool> DeletePlanAsync(Guid planId);

        // Subscription Features
        Task<SubscriptionFeatureDto> CreateFeatureAsync(CreateSubscriptionFeatureDto dto);
        Task<SubscriptionFeatureDto?> GetFeatureByIdAsync(Guid featureId);
        Task<IEnumerable<SubscriptionFeatureDto>> GetAllFeaturesAsync();
        Task<SubscriptionFeatureDto?> UpdateFeatureAsync(Guid featureId, UpdateSubscriptionFeatureDto dto);
        Task<bool> DeleteFeatureAsync(Guid featureId);

        // Plan Features (junction table)
        Task<PlanFeatureDto> AddFeatureToPlanAsync(CreatePlanFeatureDto dto);
        Task<bool> RemoveFeatureFromPlanAsync(Guid planId, Guid featureId);
        Task<IEnumerable<PlanFeatureDto>> GetPlanFeaturesAsync(Guid planId);

        // User Subscriptions
        Task<UserSubscriptionDto> CreateUserSubscriptionAsync(Guid userId, CreateUserSubscriptionDto dto);
        Task<UserSubscriptionDto?> GetUserSubscriptionAsync(Guid userId);
        Task<UserSubscriptionDto?> UpdateUserSubscriptionAsync(Guid userId, UpdateUserSubscriptionDto dto);
        Task<bool> CancelUserSubscriptionAsync(Guid userId, CancelSubscriptionDto dto);

        // Apple appAccountToken issuance (for StoreKit appAccountToken linkage)
        Task<Guid> CreateAppleAppAccountTokenAsync(Guid userId);

        // Apple verifyReceipt-style endpoint (uses same logic as ActivateAppleSubscriptionAsync)
        Task<UserSubscriptionDto> VerifyAppleReceiptAsync(Guid userId, AppleSubscribeRequest dto);

        // Subscription Overview
        Task<SubscriptionOverviewDto> GetSubscriptionOverviewAsync(Guid userId);
        
        // Utility methods
        Task<bool> HasFeatureAsync(Guid userId, string featureName);
        Task<bool> IsSubscriptionActiveAsync(Guid userId);
    }
}

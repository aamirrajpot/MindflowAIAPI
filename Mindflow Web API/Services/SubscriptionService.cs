using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(MindflowDbContext dbContext, ILogger<SubscriptionService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // -----------------------------
        // Provider-oriented operations
        // Apple now, Google later. These methods do NOT require
        // schema changes yet; they upsert the single UserSubscription
        // row using existing columns (StartDate/EndDate/Status/PlanId).
        // Table evolution will come in a later step.
        // -----------------------------

        // Activates or refreshes a subscription for Apple purchases.
        // Assumes the request was validated against Apple (JWS or Server API)
        // and includes the mapped productId and an expiry.
        public async Task<UserSubscriptionDto> ActivateAppleSubscriptionAsync(Guid userId, AppleSubscribeRequest dto)
        {
            // Map Apple product to an internal plan id
            var planId = await MapProductToPlanIdAsync(dto.ProductId, provider: SubscriptionProvider.Apple, environment: dto.Environment ?? "production");

            // Cancel any existing active subscription (single active at a time)
            var existing = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);
            if (existing != null)
            {
                existing.Status = SubscriptionStatus.Cancelled;
                existing.EndDate = DateTime.UtcNow;
            }

            var now = DateTime.UtcNow;
            var userSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = planId,
                StartDate = now,
                EndDate = dto.ExpiresAtUtc, // legacy field
                Status = SubscriptionStatus.Active,
                // Provider fields
                Provider = SubscriptionProvider.Apple,
                ProductId = dto.ProductId,
                OriginalTransactionId = dto.OriginalTransactionId,
                LatestTransactionId = dto.TransactionId,
                ExpiresAtUtc = dto.ExpiresAtUtc,
                AutoRenewEnabled = true,
                Environment = dto.Environment ?? "production",
                AppAccountToken = dto.AppAccountToken,
                RawTransactionPayload = dto.SignedTransactionJws,
                RawRenewalPayload = dto.SignedRenewalInfoJws
            };

            await _dbContext.UserSubscriptions.AddAsync(userSubscription);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Apple subscription activated for user {UserId} with product {ProductId}", userId, dto.ProductId);
            return await ToUserSubscriptionDtoAsync(userSubscription);
        }

        // Restores an Apple subscription (e.g., re-install, new device)
        // Fetches latest state from Apple and mirrors it into our single row model.
        public async Task<UserSubscriptionDto?> RestoreAppleSubscriptionAsync(Guid userId, AppleRestoreRequest dto)
        {
            // Resolve plan id from product
            var planId = await MapProductToPlanIdAsync(dto.ProductId, provider: SubscriptionProvider.Apple, environment: dto.Environment ?? "production");

            // Find active subscription for this user
            var current = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (current == null)
            {
                // Nothing active; create new from restore payload
                var now = DateTime.UtcNow;
                var created = new UserSubscription
                {
                    UserId = userId,
                    PlanId = planId,
                    StartDate = now,
                    EndDate = dto.ExpiresAtUtc,
                    Status = SubscriptionStatus.Active
                };
                await _dbContext.UserSubscriptions.AddAsync(created);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Apple subscription restored for user {UserId} with product {ProductId}", userId, dto.ProductId);
                return await ToUserSubscriptionDtoAsync(created);
            }

            // Update existing active row
            current.PlanId = planId;
            current.EndDate = dto.ExpiresAtUtc; // legacy
            current.Status = SubscriptionStatus.Active;
            current.Provider = SubscriptionProvider.Apple;
            current.ProductId = dto.ProductId;
            current.OriginalTransactionId = dto.OriginalTransactionId;
            current.LatestTransactionId = dto.OriginalTransactionId; // if unknown, keep original
            current.ExpiresAtUtc = dto.ExpiresAtUtc;
            current.AutoRenewEnabled = true;
            current.Environment = dto.Environment ?? "production";
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Apple subscription refreshed for user {UserId} with product {ProductId}", userId, dto.ProductId);
            return await ToUserSubscriptionDtoAsync(current);
        }

        // Placeholder for Apple Server Notification processing.
        // For the current schema, we only adjust EndDate/Status.
        public Task<bool> ApplyAppleNotificationAsync(AppleNotificationDto notification)
        {
            // Implementation placeholder: in the next steps, when provider columns are added,
            // we will locate the correct subscription by original transaction id.
            // For now, we log and no-op to keep flow incremental.
            _logger.LogInformation("Received Apple server notification payload (length {Len})", notification.SignedPayload?.Length ?? 0);
            return Task.FromResult(true);
        }

        // Subscription Plans
        public async Task<SubscriptionPlanDto> CreatePlanAsync(CreateSubscriptionPlanDto dto)
        {
            var plan = new SubscriptionPlan
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                BillingCycle = dto.BillingCycle,
                IsActive = dto.IsActive,
                SortOrder = dto.SortOrder,
                OriginalPrice = dto.OriginalPrice,
                IsPopular = dto.IsPopular
            };

            await _dbContext.SubscriptionPlans.AddAsync(plan);
            await _dbContext.SaveChangesAsync();

            return await ToPlanDtoAsync(plan);
        }

        public async Task<SubscriptionPlanDto?> GetPlanByIdAsync(Guid planId)
        {
            var plan = await _dbContext.SubscriptionPlans
                .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .FirstOrDefaultAsync(p => p.Id == planId);

            return plan == null ? null : await ToPlanDtoAsync(plan);
        }

        public async Task<IEnumerable<SubscriptionPlanDto>> GetAllPlansAsync()
        {
            var plans = await _dbContext.SubscriptionPlans
                .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            var planDtos = new List<SubscriptionPlanDto>();
            foreach (var plan in plans)
            {
                planDtos.Add(await ToPlanDtoAsync(plan));
            }

            return planDtos;
        }

        public async Task<SubscriptionPlanDto?> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanDto dto)
        {
            var plan = await _dbContext.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null) return null;

            if (dto.Name != null) plan.Name = dto.Name;
            if (dto.Description != null) plan.Description = dto.Description;
            if (dto.Price.HasValue) plan.Price = dto.Price.Value;
            if (dto.BillingCycle != null) plan.BillingCycle = dto.BillingCycle;
            if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;
            if (dto.SortOrder.HasValue) plan.SortOrder = dto.SortOrder.Value;
            if (dto.OriginalPrice != null) plan.OriginalPrice = dto.OriginalPrice;
            if (dto.IsPopular.HasValue) plan.IsPopular = dto.IsPopular.Value;

            await _dbContext.SaveChangesAsync();
            return await ToPlanDtoAsync(plan);
        }

        public async Task<bool> DeletePlanAsync(Guid planId)
        {
            var plan = await _dbContext.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null) return false;

            _dbContext.SubscriptionPlans.Remove(plan);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // Subscription Features
        public async Task<SubscriptionFeatureDto> CreateFeatureAsync(CreateSubscriptionFeatureDto dto)
        {
            var feature = new SubscriptionFeature
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive,
                SortOrder = dto.SortOrder,
                Icon = dto.Icon
            };

            await _dbContext.SubscriptionFeatures.AddAsync(feature);
            await _dbContext.SaveChangesAsync();

            return ToFeatureDto(feature);
        }

        public async Task<SubscriptionFeatureDto?> GetFeatureByIdAsync(Guid featureId)
        {
            var feature = await _dbContext.SubscriptionFeatures.FirstOrDefaultAsync(f => f.Id == featureId);
            return feature == null ? null : ToFeatureDto(feature);
        }

        public async Task<IEnumerable<SubscriptionFeatureDto>> GetAllFeaturesAsync()
        {
            var features = await _dbContext.SubscriptionFeatures
                .Where(f => f.IsActive)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            return features.Select(ToFeatureDto);
        }

        public async Task<SubscriptionFeatureDto?> UpdateFeatureAsync(Guid featureId, UpdateSubscriptionFeatureDto dto)
        {
            var feature = await _dbContext.SubscriptionFeatures.FirstOrDefaultAsync(f => f.Id == featureId);
            if (feature == null) return null;

            if (dto.Name != null) feature.Name = dto.Name;
            if (dto.Description != null) feature.Description = dto.Description;
            if (dto.IsActive.HasValue) feature.IsActive = dto.IsActive.Value;
            if (dto.SortOrder.HasValue) feature.SortOrder = dto.SortOrder.Value;
            if (dto.Icon != null) feature.Icon = dto.Icon;

            await _dbContext.SaveChangesAsync();
            return ToFeatureDto(feature);
        }

        public async Task<bool> DeleteFeatureAsync(Guid featureId)
        {
            var feature = await _dbContext.SubscriptionFeatures.FirstOrDefaultAsync(f => f.Id == featureId);
            if (feature == null) return false;

            _dbContext.SubscriptionFeatures.Remove(feature);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // Plan Features
        public async Task<PlanFeatureDto> AddFeatureToPlanAsync(CreatePlanFeatureDto dto)
        {
            var planFeature = new PlanFeature
            {
                PlanId = dto.PlanId,
                FeatureId = dto.FeatureId,
                IsIncluded = dto.IsIncluded,
                Limit = dto.Limit
            };

            await _dbContext.PlanFeatures.AddAsync(planFeature);
            await _dbContext.SaveChangesAsync();

            return await ToPlanFeatureDtoAsync(planFeature);
        }

        public async Task<bool> RemoveFeatureFromPlanAsync(Guid planId, Guid featureId)
        {
            var planFeature = await _dbContext.PlanFeatures
                .FirstOrDefaultAsync(pf => pf.PlanId == planId && pf.FeatureId == featureId);

            if (planFeature == null) return false;

            _dbContext.PlanFeatures.Remove(planFeature);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<PlanFeatureDto>> GetPlanFeaturesAsync(Guid planId)
        {
            var planFeatures = await _dbContext.PlanFeatures
                .Include(pf => pf.Feature)
                .Where(pf => pf.PlanId == planId && pf.IsIncluded)
                .OrderBy(pf => pf.Feature.SortOrder)
                .ToListAsync();

            var planFeatureDtos = new List<PlanFeatureDto>();
            foreach (var planFeature in planFeatures)
            {
                planFeatureDtos.Add(await ToPlanFeatureDtoAsync(planFeature));
            }

            return planFeatureDtos;
        }

        // User Subscriptions
        public async Task<UserSubscriptionDto> CreateUserSubscriptionAsync(Guid userId, CreateUserSubscriptionDto dto)
        {
            // Cancel any existing active subscription
            var existingSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (existingSubscription != null)
            {
                existingSubscription.Status = SubscriptionStatus.Cancelled;
                existingSubscription.EndDate = DateTime.UtcNow;
            }

            var userSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = dto.PlanId,
                StartDate = DateTime.UtcNow,
                Status = SubscriptionStatus.Active
            };

            await _dbContext.UserSubscriptions.AddAsync(userSubscription);
            await _dbContext.SaveChangesAsync();

            return await ToUserSubscriptionDtoAsync(userSubscription);
        }

        public async Task<UserSubscriptionDto?> GetUserSubscriptionAsync(Guid userId)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .Include(us => us.Plan)
                .ThenInclude(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            return userSubscription == null ? null : await ToUserSubscriptionDtoAsync(userSubscription);
        }

        public async Task<UserSubscriptionDto?> UpdateUserSubscriptionAsync(Guid userId, UpdateUserSubscriptionDto dto)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (userSubscription == null) return null;

            if (dto.PlanId.HasValue) userSubscription.PlanId = dto.PlanId.Value;
            if (dto.EndDate.HasValue) userSubscription.EndDate = dto.EndDate.Value;
            if (dto.Status.HasValue) userSubscription.Status = dto.Status.Value;

            await _dbContext.SaveChangesAsync();
            return await ToUserSubscriptionDtoAsync(userSubscription);
        }

        public async Task<bool> CancelUserSubscriptionAsync(Guid userId, CancelSubscriptionDto dto)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (userSubscription == null) return false;

            userSubscription.Status = SubscriptionStatus.Cancelled;
            userSubscription.EndDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        // Subscription Overview
        public async Task<SubscriptionOverviewDto> GetSubscriptionOverviewAsync(Guid userId)
        {
            var currentSubscription = await GetUserSubscriptionAsync(userId);
            var availablePlans = await GetAllPlansAsync();
            var allFeatures = await GetAllFeaturesAsync();

            return new SubscriptionOverviewDto(
                currentSubscription,
                availablePlans.ToList(),
                allFeatures.ToList()
            );
        }

        // Utility methods
        public async Task<bool> HasFeatureAsync(Guid userId, string featureName)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .Include(us => us.Plan)
                .ThenInclude(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (userSubscription == null) return false;

            return userSubscription.Plan.PlanFeatures
                .Any(pf => pf.IsIncluded && pf.Feature.Name == featureName);
        }

        public async Task<bool> IsSubscriptionActiveAsync(Guid userId)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            return userSubscription != null;
        }

        // Private helper methods
        private async Task<SubscriptionPlanDto> ToPlanDtoAsync(SubscriptionPlan plan)
        {
            var features = await GetPlanFeaturesAsync(plan.Id);
            var featureDtos = features.Select(pf => pf.Feature).ToList();

            return new SubscriptionPlanDto(
                plan.Id,
                plan.Name,
                plan.Description,
                plan.Price,
                plan.BillingCycle,
                plan.IsActive,
                plan.SortOrder,
                plan.OriginalPrice,
                plan.IsPopular,
                featureDtos
            );
        }

        private static SubscriptionFeatureDto ToFeatureDto(SubscriptionFeature feature)
        {
            return new SubscriptionFeatureDto(
                feature.Id,
                feature.Name,
                feature.Description,
                feature.IsActive,
                feature.SortOrder,
                feature.Icon
            );
        }

        private async Task<PlanFeatureDto> ToPlanFeatureDtoAsync(PlanFeature planFeature)
        {
            return new PlanFeatureDto(
                planFeature.Id,
                planFeature.PlanId,
                planFeature.FeatureId,
                planFeature.IsIncluded,
                planFeature.Limit,
                ToFeatureDto(planFeature.Feature)
            );
        }

        private async Task<UserSubscriptionDto> ToUserSubscriptionDtoAsync(UserSubscription userSubscription)
        {
            // Ensure the Plan entity is loaded
            SubscriptionPlan? planEntity = userSubscription.Plan;
            if (planEntity == null)
            {
                planEntity = await _dbContext.SubscriptionPlans
                    .FirstOrDefaultAsync(p => p.Id == userSubscription.PlanId);
            }

            // Fallback in extremely rare case plan is missing (should not happen)
            if (planEntity == null)
            {
                var emptyPlanDto = new SubscriptionPlanDto(
                    userSubscription.PlanId,
                    string.Empty,
                    string.Empty,
                    0,
                    string.Empty,
                    false,
                    0,
                    null,
                    false,
                    new List<SubscriptionFeatureDto>()
                );

                return new UserSubscriptionDto(
                    userSubscription.Id,
                    userSubscription.UserId,
                    userSubscription.PlanId,
                    userSubscription.StartDate,
                    userSubscription.EndDate,
                    userSubscription.Status,
                    emptyPlanDto
                );
            }

            var plan = await ToPlanDtoAsync(planEntity);

            return new UserSubscriptionDto(
                userSubscription.Id,
                userSubscription.UserId,
                userSubscription.PlanId,
                userSubscription.StartDate,
                userSubscription.EndDate,
                userSubscription.Status,
                plan
            );
        }

        // Maps a store product identifier to an internal plan id.
        // Temporary strategy: match by plan Name; refine later with explicit mapping table.
        private async Task<Guid> MapProductToPlanIdAsync(string productId, SubscriptionProvider provider, string environment)
        {
            var map = await _dbContext.Set<StoreProduct>()
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.ProductId == productId && sp.Provider == provider && sp.Environment == environment);
            if (map == null)
                throw new InvalidOperationException($"No StoreProduct mapping for {provider} product '{productId}' in env '{environment}'.");
            return map.PlanId;
        }
    }
}

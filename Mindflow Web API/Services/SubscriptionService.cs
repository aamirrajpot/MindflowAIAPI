using Microsoft.EntityFrameworkCore;
using Mimo.AppStoreServerLibrary;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly SignedDataVerifier _appleVerifier;

        public SubscriptionService(MindflowDbContext dbContext, ILogger<SubscriptionService> logger, SignedDataVerifier appleVerifier)
        {
            _dbContext = dbContext;
            _logger = logger;
            _appleVerifier = appleVerifier;
        }

        private static byte[] Base64UrlDecode(string input)
        {
            // Replace URL-safe chars and pad with '='
            var padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
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
                PlanId = dto.ProductId, // Use productId directly as PlanId
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
                    PlanId = dto.ProductId, // Use productId directly as PlanId
                    StartDate = now,
                    EndDate = dto.ExpiresAtUtc,
                    Status = SubscriptionStatus.Active,
                    Provider = SubscriptionProvider.Apple,
                    ProductId = dto.ProductId,
                    OriginalTransactionId = dto.OriginalTransactionId,
                    LatestTransactionId = dto.OriginalTransactionId,
                    ExpiresAtUtc = dto.ExpiresAtUtc,
                    AutoRenewEnabled = true,
                    Environment = dto.Environment ?? "production"
                };
                await _dbContext.UserSubscriptions.AddAsync(created);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Apple subscription restored for user {UserId} with product {ProductId}", userId, dto.ProductId);
                return await ToUserSubscriptionDtoAsync(created);
            }

            // Update existing active row
            current.PlanId = dto.ProductId; // Use productId directly as PlanId
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

        // Apple Server Notification processing (ASN v2).
        // NOTE: This implementation decodes the JWS payloads and updates UserSubscriptions,
        // but does NOT yet perform full cryptographic signature verification.
        public async Task<bool> ApplyAppleNotificationAsync(AppleNotificationDto notification)
        {
            if (string.IsNullOrWhiteSpace(notification.SignedPayload))
            {
                _logger.LogWarning("Apple notification received with empty signed payload.");
                return false;
            }

            // Log complete payload for local debugging
            _logger.LogInformation("Apple notification complete signedPayload: {SignedPayload}", notification.SignedPayload);

            try
            {
                // 0) Cryptographically verify the JWS using Apple's root certificates via Mimo.AppStoreServerLibrary
                // We ignore the decoded model here and reuse our existing parsing logic below.
                _logger.LogInformation("Verifying Apple notification signature...");
                try
                {
                    await _appleVerifier.VerifyAndDecodeNotification(notification.SignedPayload);
                    _logger.LogInformation("Apple notification signature verification succeeded.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Apple signedPayload verification failed.");
                    return false;
                }

                // 1) Decode outer JWS payload (header.payload.signature)
                _logger.LogInformation("Decoding outer JWS payload...");
                var outerParts = notification.SignedPayload.Split('.');
                if (outerParts.Length != 3)
                {
                    _logger.LogWarning("Apple notification signedPayload has invalid JWS format (expected 3 parts, got {Count}).", outerParts.Length);
                    return false;
                }

                var outerPayloadJson = Encoding.UTF8.GetString(Base64UrlDecode(outerParts[1]));
                using var outerDoc = JsonDocument.Parse(outerPayloadJson);
                var outerRoot = outerDoc.RootElement;

                var notificationType = outerRoot.GetProperty("notificationType").GetString() ?? string.Empty;
                var environment = outerRoot.TryGetProperty("environment", out var envEl)
                    ? envEl.GetString() ?? "sandbox"
                    : "sandbox";

                _logger.LogInformation("Processing Apple notification: Type={NotificationType}, Environment={Environment}", notificationType, environment);

                if (!outerRoot.TryGetProperty("data", out var dataEl))
                {
                    _logger.LogWarning("Apple notification missing 'data' element.");
                    return false;
                }

                // 2) Decode signedTransactionInfo JWS to get transaction details
                if (!dataEl.TryGetProperty("signedTransactionInfo", out var signedTxEl))
                {
                    _logger.LogWarning("Apple notification data missing 'signedTransactionInfo'.");
                    return false;
                }

                var signedTransactionInfo = signedTxEl.GetString();
                if (string.IsNullOrWhiteSpace(signedTransactionInfo))
                {
                    _logger.LogWarning("Apple notification 'signedTransactionInfo' is empty.");
                    return false;
                }

                var txParts = signedTransactionInfo.Split('.');
                if (txParts.Length != 3)
                {
                    _logger.LogWarning("Apple transaction JWS has invalid format.");
                    return false;
                }

                _logger.LogInformation("Decoding signedTransactionInfo JWS...");
                var txPayloadJson = Encoding.UTF8.GetString(Base64UrlDecode(txParts[1]));
                using var txDoc = JsonDocument.Parse(txPayloadJson);
                var txRoot = txDoc.RootElement;

                // Log complete transaction root JSON for debugging
                _logger.LogInformation("Transaction root (txRoot) complete JSON: {TxRootJson}", txRoot.GetRawText());

                var originalTransactionId = txRoot.GetProperty("originalTransactionId").GetString();
                var transactionId = txRoot.TryGetProperty("transactionId", out var txIdEl)
                    ? txIdEl.GetString()
                    : null;
                var productId = txRoot.TryGetProperty("productId", out var prodEl)
                    ? prodEl.GetString()
                    : null;

                DateTime? expiresAtUtc = null;
                if (txRoot.TryGetProperty("expiresDate", out var expiresEl) && expiresEl.ValueKind == JsonValueKind.Number)
                {
                    // Apple uses milliseconds since Unix epoch
                    if (expiresEl.TryGetInt64(out var ms))
                    {
                        expiresAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                    }
                }

                _logger.LogInformation(
                    "Extracted transaction details: OriginalTransactionId={OriginalTransactionId}, TransactionId={TransactionId}, ProductId={ProductId}, ExpiresAtUtc={ExpiresAtUtc}",
                    originalTransactionId,
                    transactionId ?? "null",
                    productId ?? "null",
                    expiresAtUtc?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "null");

                if (string.IsNullOrWhiteSpace(originalTransactionId))
                {
                    _logger.LogWarning("Apple transaction payload missing originalTransactionId.");
                    return false;
                }

                // 3) Find or create UserSubscription for this originalTransactionId (Apple)
                _logger.LogInformation("Looking up UserSubscription for OriginalTransactionId={OriginalTransactionId}...", originalTransactionId);
                var subscription = await _dbContext.UserSubscriptions
                    .FirstOrDefaultAsync(s =>
                        s.Provider == SubscriptionProvider.Apple &&
                        s.OriginalTransactionId == originalTransactionId);

                if (subscription == null)
                {
                    _logger.LogInformation("No existing subscription found. Creating new UserSubscription for OriginalTransactionId={OriginalTransactionId}.", originalTransactionId);
                    subscription = new UserSubscription
                    {
                        UserId = Guid.Empty, // To be associated via appAccountToken / client flows
                        PlanId = productId ?? string.Empty, // Use productId from Apple as PlanId
                        Provider = SubscriptionProvider.Apple,
                        ProductId = productId ?? string.Empty,
                        OriginalTransactionId = originalTransactionId,
                        StartDate = DateTime.UtcNow,
                        Environment = environment
                    };
                    await _dbContext.UserSubscriptions.AddAsync(subscription);
                }
                else
                {
                    _logger.LogInformation("Found existing subscription: SubscriptionId={SubscriptionId}, CurrentStatus={Status}, CurrentProductId={ProductId}",
                        subscription.Id, subscription.Status, subscription.ProductId);
                }

                _logger.LogInformation("Updating subscription fields: LatestTransactionId={LatestTransactionId}, ProductId={ProductId}, ExpiresAtUtc={ExpiresAtUtc}, Environment={Environment}",
                    transactionId ?? subscription.LatestTransactionId ?? "null",
                    productId ?? subscription.ProductId ?? "null",
                    expiresAtUtc?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "null",
                    environment);

                subscription.LatestTransactionId = transactionId ?? subscription.LatestTransactionId;
                subscription.ProductId = productId ?? subscription.ProductId;
                subscription.PlanId = productId ?? subscription.PlanId; // Keep PlanId in sync with ProductId
                subscription.ExpiresAtUtc = expiresAtUtc;
                subscription.Environment = environment;
                subscription.RawNotificationPayload = notification.SignedPayload; // Complete outer signedPayload for debugging
                subscription.RawTransactionPayload = signedTransactionInfo; // Inner signedTransactionInfo JWS

                // Sync EndDate with ExpiresAtUtc if ExpiresAtUtc is set and EndDate is null (for active subscriptions)
                if (expiresAtUtc.HasValue && subscription.Status == SubscriptionStatus.Active && !subscription.EndDate.HasValue)
                {
                    subscription.EndDate = expiresAtUtc.Value;
                    _logger.LogInformation("Syncing EndDate with ExpiresAtUtc: {ExpiresAtUtc}", expiresAtUtc.Value);
                }

                // Extract renewal info if available (needed for DID_CHANGE_RENEWAL_PREF)
                bool? autoRenewStatus = null;
                string? signedRenewalInfo = null;
                if (dataEl.TryGetProperty("signedRenewalInfo", out var signedRenewalEl) && signedRenewalEl.ValueKind == JsonValueKind.String)
                {
                    signedRenewalInfo = signedRenewalEl.GetString();
                    subscription.RawRenewalPayload = signedRenewalInfo;

                    if (!string.IsNullOrWhiteSpace(signedRenewalInfo))
                    {
                        try
                        {
                            var renewalParts = signedRenewalInfo.Split('.');
                            if (renewalParts.Length == 3)
                            {
                                var renewalPayloadJson = Encoding.UTF8.GetString(Base64UrlDecode(renewalParts[1]));
                                using var renewalDoc = JsonDocument.Parse(renewalPayloadJson);
                                var renewalRoot = renewalDoc.RootElement;

                                _logger.LogInformation("Renewal info (signedRenewalInfo) complete JSON: {RenewalInfoJson}", renewalRoot.GetRawText());

                                if (renewalRoot.TryGetProperty("autoRenewStatus", out var autoRenewEl))
                                {
                                    var autoRenewValue = autoRenewEl.GetInt32();
                                    // Apple: 0 = Off, 1 = On
                                    autoRenewStatus = autoRenewValue == 1;
                                    _logger.LogInformation("Extracted autoRenewStatus from renewal info: {AutoRenewStatus} (value={Value})", autoRenewStatus, autoRenewValue);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to decode signedRenewalInfo, continuing without renewal status update.");
                        }
                    }
                }

                // 4) Map notificationType -> subscription status
                var previousStatus = subscription.Status;
                _logger.LogInformation("Processing notification type: {NotificationType} (previous status: {PreviousStatus})", notificationType, previousStatus);

                switch (notificationType.ToUpperInvariant())
                {
                    case "SUBSCRIBED":
                        _logger.LogInformation("Processing SUBSCRIBED notification: Activating subscription.");
                        subscription.Status = SubscriptionStatus.Active;
                        subscription.EndDate = expiresAtUtc;
                        break;

                    case "DID_RENEW":
                        _logger.LogInformation("Processing DID_RENEW notification: Renewing subscription.");
                        subscription.Status = SubscriptionStatus.Active;
                        subscription.EndDate = expiresAtUtc;
                        break;

                    case "EXPIRED":
                        _logger.LogInformation("Processing EXPIRED notification: Marking subscription as expired.");
                        subscription.Status = SubscriptionStatus.Expired;
                        subscription.EndDate = expiresAtUtc ?? DateTime.UtcNow;
                        break;

                    case "DID_FAIL_TO_RENEW":
                        _logger.LogInformation("Processing DID_FAIL_TO_RENEW notification: Marking subscription with billing retry status.");
                        subscription.Status = SubscriptionStatus.BillingRetry;
                        break;

                    case "REFUND":
                        _logger.LogInformation("Processing REFUND notification: Cancelling subscription due to refund.");
                        subscription.Status = SubscriptionStatus.Cancelled;
                        break;

                    case "REVOKE":
                        _logger.LogInformation("Processing REVOKE notification: Cancelling subscription due to revocation.");
                        subscription.Status = SubscriptionStatus.Cancelled;
                        break;

                    case "DID_CHANGE_RENEWAL_PREF":
                        _logger.LogInformation("Processing DID_CHANGE_RENEWAL_PREF notification: User changed renewal preferences.");
                        if (autoRenewStatus.HasValue)
                        {
                            var previousAutoRenew = subscription.AutoRenewEnabled;
                            subscription.AutoRenewEnabled = autoRenewStatus.Value;
                            _logger.LogInformation("Auto-renewal status updated: {PreviousAutoRenew} -> {NewAutoRenew}", previousAutoRenew, autoRenewStatus.Value);
                        }
                        else
                        {
                            _logger.LogWarning("DID_CHANGE_RENEWAL_PREF received but could not extract autoRenewStatus from renewal info.");
                        }
                        // Update EndDate if ExpiresAtUtc is available (renewal preference change might come with updated expiry)
                        if (expiresAtUtc.HasValue)
                        {
                            subscription.EndDate = expiresAtUtc.Value;
                        }
                        // Status remains unchanged for renewal preference changes
                        break;

                    default:
                        _logger.LogWarning("Unhandled Apple notification type: {Type}. Subscription status unchanged.", notificationType);
                        break;
                }

                _logger.LogInformation("Status updated: {PreviousStatus} -> {NewStatus}", previousStatus, subscription.Status);

                _logger.LogInformation("Saving subscription changes to database...");
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Subscription saved successfully.");

                _logger.LogInformation(
                    "✅ Successfully processed Apple notification: Type={Type}, OriginalTransactionId={OriginalTransactionId}, ProductId={ProductId}, Status={Status}, ExpiresAtUtc={ExpiresAtUtc}, AutoRenewEnabled={AutoRenewEnabled}",
                    notificationType,
                    originalTransactionId,
                    productId ?? "null",
                    subscription.Status,
                    subscription.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "null",
                    subscription.AutoRenewEnabled);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Apple server notification.");
                return false;
            }
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
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            return userSubscription == null ? null : await ToUserSubscriptionDtoAsync(userSubscription);
        }

        public async Task<UserSubscriptionDto?> UpdateUserSubscriptionAsync(Guid userId, UpdateUserSubscriptionDto dto)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (userSubscription == null) return null;

            if (!string.IsNullOrWhiteSpace(dto.PlanId)) userSubscription.PlanId = dto.PlanId;
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
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (userSubscription == null) return false;

            // Resolve plan via StoreProduct mapping since PlanId is now a productId string
            try
            {
                var storeProduct = await _dbContext.Set<StoreProduct>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sp => sp.ProductId == userSubscription.PlanId && sp.Provider == userSubscription.Provider);

                if (storeProduct != null)
                {
                    var plan = await _dbContext.SubscriptionPlans
                        .Include(p => p.PlanFeatures)
                        .ThenInclude(pf => pf.Feature)
                        .FirstOrDefaultAsync(p => p.Id == storeProduct.PlanId);

                    if (plan != null)
                    {
                        return plan.PlanFeatures
                            .Any(pf => pf.IsIncluded && pf.Feature.Name == featureName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve plan for HasFeatureAsync check. PlanId={PlanId}", userSubscription.PlanId);
            }

            return false;
        }

        public async Task<bool> IsSubscriptionActiveAsync(Guid userId)
        {
            var userSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);

            if (userSubscription == null)
                return false;

            // Check if subscription has expired
            var now = DateTime.UtcNow;
            var expiryDate = userSubscription.EndDate ?? userSubscription.ExpiresAtUtc;
            
            if (expiryDate.HasValue && expiryDate.Value < now)
            {
                // Subscription has expired, update status
                userSubscription.Status = SubscriptionStatus.Expired;
                await _dbContext.SaveChangesAsync();
                return false;
            }

            return true;
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
            // PlanId is now a string (productId), not a Guid reference to SubscriptionPlan
            // Try to find a matching SubscriptionPlan by matching PlanId with StoreProduct mapping
            SubscriptionPlanDto? planDto = null;
            
            try
            {
                // Try to find StoreProduct mapping to get internal plan
                var storeProduct = await _dbContext.Set<StoreProduct>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sp => sp.ProductId == userSubscription.PlanId && sp.Provider == userSubscription.Provider);
                
                if (storeProduct != null)
                {
                    var planEntity = await _dbContext.SubscriptionPlans
                        .Include(p => p.PlanFeatures)
                        .ThenInclude(pf => pf.Feature)
                        .FirstOrDefaultAsync(p => p.Id == storeProduct.PlanId);
                    
                    if (planEntity != null)
                    {
                        planDto = await ToPlanDtoAsync(planEntity);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve SubscriptionPlan for PlanId={PlanId}, returning null Plan in DTO.", userSubscription.PlanId);
            }

            // Use ExpiresAtUtc as fallback if EndDate is null (for Apple/Google subscriptions)
            var endDate = userSubscription.EndDate ?? userSubscription.ExpiresAtUtc;

            return new UserSubscriptionDto(
                userSubscription.Id,
                userSubscription.UserId,
                userSubscription.PlanId, // String productId
                userSubscription.StartDate,
                endDate,
                userSubscription.Status,
                planDto, // May be null if no mapping exists
                // Additional subscription details
                userSubscription.Provider, // Apple or Google
                userSubscription.ProductId, // Product identifier
                userSubscription.ExpiresAtUtc, // Authoritative expiry from store
                userSubscription.AutoRenewEnabled, // Auto-renewal status
                userSubscription.Environment // "production" or "sandbox"
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

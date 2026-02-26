using Microsoft.EntityFrameworkCore;
using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Models;
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
        private readonly AppleAppStoreApiWrapper _appleApiWrapper;
        private readonly ReceiptUtility _receiptUtility;

        public SubscriptionService(
            MindflowDbContext dbContext,
            ILogger<SubscriptionService> logger,
            SignedDataVerifier appleVerifier,
            AppleAppStoreApiWrapper appleApiWrapper,
            ReceiptUtility receiptUtility)
        {
            _dbContext = dbContext;
            _logger = logger;
            _appleVerifier = appleVerifier;
            _appleApiWrapper = appleApiWrapper;
            _receiptUtility = receiptUtility;
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

        /// <summary>
        /// Verifies a legacy (PKCS#7) receipt using only Mimo.AppStoreServerLibrary:
        /// ReceiptUtility extracts transaction ID → App Store Server API Get Transaction Info → SignedDataVerifier.VerifyAndDecodeTransaction.
        /// </summary>
        private async Task<(string originalTransactionId, string productId, DateTime? expiresAtUtc, string environment, string transactionId, Guid? appAccountToken)> VerifyLegacyReceiptWithMimoAsync(string receiptBase64)
        {
            string transactionId;
            try
            {
                transactionId = _receiptUtility.ExtractTransactionIdFromAppReceipt(receiptBase64);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract transaction ID from legacy receipt.");
                throw new InvalidOperationException("Invalid receipt format. The transaction receipt could not be parsed.", ex);
            }

            if (string.IsNullOrWhiteSpace(transactionId))
            {
                _logger.LogError("ReceiptUtility returned empty transaction ID.");
                throw new InvalidOperationException("Receipt did not contain a valid transaction ID.");
            }

            var response = await _appleApiWrapper.GetTransactionInfoAsync(transactionId);
            if (response == null || string.IsNullOrWhiteSpace(response.SignedTransactionInfo))
            {
                _logger.LogError("Get Transaction Info returned no signed transaction for TransactionId={TransactionId}.", transactionId);
                throw new InvalidOperationException("Apple did not return transaction information for this receipt. The transaction may be invalid or from a different app.");
            }

            JwsTransactionDecodedPayload decoded;
            try
            {
                decoded = await _appleVerifier.VerifyAndDecodeTransaction(response.SignedTransactionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify/decode signed transaction from Apple.");
                throw new InvalidOperationException("Transaction signature verification failed.", ex);
            }

            DateTime? expiresAtUtc = decoded.ExpiresDate > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(decoded.ExpiresDate).UtcDateTime
                : (DateTime?)null;
            Guid? appAccountToken = null;
            if (!string.IsNullOrWhiteSpace(decoded.AppAccountToken) && Guid.TryParse(decoded.AppAccountToken, out var parsed))
                appAccountToken = parsed;

            return (decoded.OriginalTransactionId, decoded.ProductId, expiresAtUtc, decoded.Environment ?? "production", decoded.TransactionId, appAccountToken);
        }

        // -----------------------------
        // Provider-oriented operations
        // Apple now, Google later. These methods do NOT require
        // schema changes yet; they upsert the single UserSubscription
        // row using existing columns (StartDate/EndDate/Status/PlanId).
        // Table evolution will come in a later step.
        // -----------------------------

        // Activates or refreshes a subscription for Apple purchases.
        // Supports (1) JWS from StoreKit 2 or (2) legacy transaction receipt verified via Mimo (ReceiptUtility + App Store Server API + SignedDataVerifier).
        public async Task<UserSubscriptionDto> ActivateAppleSubscriptionAsync(Guid userId, AppleSubscribeRequest dto)
        {
            string finalProductId;
            string finalOriginalTransactionId;
            string finalTransactionId = dto.TransactionId;
            DateTime? finalExpiresAtUtc;
            Guid? finalAppAccountToken = dto.AppAccountToken;
            string environment = dto.Environment ?? "production";

            if (!string.IsNullOrWhiteSpace(dto.TransactionReceipt))
            {
                // Legacy PKCS#7 receipt path – fully verify with Mimo (ReceiptUtility + App Store Server API + SignedDataVerifier)
                _logger.LogInformation("Verifying legacy Apple transaction receipt via Mimo for user {UserId}...", userId);

                var (verifiedOriginalTransactionId,
                     verifiedProductId,
                     verifiedExpiresAtUtc,
                     verifiedEnvironment,
                     verifiedTransactionId,
                     verifiedAppAccountToken) = await VerifyLegacyReceiptWithMimoAsync(dto.TransactionReceipt);

                // Optional safety checks against client-supplied values
                if (!string.IsNullOrWhiteSpace(dto.ProductId) && dto.ProductId != verifiedProductId)
                {
                    _logger.LogWarning("Legacy receipt ProductId mismatch. Decoded={Decoded}, Client={Client}", verifiedProductId, dto.ProductId);
                }

                finalProductId = verifiedProductId;
                finalOriginalTransactionId = verifiedOriginalTransactionId;
                finalTransactionId = verifiedTransactionId;
                finalExpiresAtUtc = verifiedExpiresAtUtc;
                environment = verifiedEnvironment ?? environment;
                if (verifiedAppAccountToken.HasValue)
                    finalAppAccountToken = verifiedAppAccountToken;
            }
            else if (!string.IsNullOrWhiteSpace(dto.SignedTransactionJws))
            {
                // JWS path (StoreKit 2)
                string? verifiedProductId = null;
                string? verifiedOriginalTransactionId = null;
                string? verifiedTransactionId = null;
                DateTime? verifiedExpiresAtUtc = null;
                Guid? verifiedAppAccountToken = null;

                try
                {
                    _logger.LogInformation("Verifying Apple transaction JWS for user {UserId}...", userId);
                    var txParts = dto.SignedTransactionJws.Split('.');
                    if (txParts.Length != 3)
                        throw new InvalidOperationException("Invalid transaction JWS format. Expected 3 parts (header.payload.signature).");

                    var txPayloadJson = Encoding.UTF8.GetString(Base64UrlDecode(txParts[1]));
                    using var txDoc = JsonDocument.Parse(txPayloadJson);
                    var txRoot = txDoc.RootElement;

                    verifiedProductId = txRoot.TryGetProperty("productId", out var prodEl) ? prodEl.GetString() : null;
                    verifiedOriginalTransactionId = txRoot.TryGetProperty("originalTransactionId", out var origTxEl) ? origTxEl.GetString() : null;
                    verifiedTransactionId = txRoot.TryGetProperty("transactionId", out var txIdEl) ? txIdEl.GetString() : null;
                    if (txRoot.TryGetProperty("expiresDate", out var expiresEl) && expiresEl.ValueKind == JsonValueKind.Number && expiresEl.TryGetInt64(out var ms))
                        verifiedExpiresAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                    if (txRoot.TryGetProperty("appAccountToken", out var appTokenEl) && appTokenEl.ValueKind == JsonValueKind.String)
                    {
                        var appTokenStr = appTokenEl.GetString();
                        if (!string.IsNullOrWhiteSpace(appTokenStr) && Guid.TryParse(appTokenStr, out var parsedToken))
                            verifiedAppAccountToken = parsedToken;
                    }

                    if (!string.IsNullOrWhiteSpace(verifiedProductId) && verifiedProductId != dto.ProductId)
                        throw new InvalidOperationException($"ProductId mismatch. Decoded: {verifiedProductId}, Client: {dto.ProductId}");
                    if (!string.IsNullOrWhiteSpace(verifiedOriginalTransactionId) && verifiedOriginalTransactionId != dto.OriginalTransactionId)
                        throw new InvalidOperationException($"OriginalTransactionId mismatch. Decoded: {verifiedOriginalTransactionId}, Client: {dto.OriginalTransactionId}");

                    _logger.LogInformation("Apple transaction JWS decoded successfully.");
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to decode Apple transaction JWS for user {UserId}.", userId);
                    throw new InvalidOperationException("Failed to decode Apple transaction receipt. The transaction JWS may be invalid or corrupted.", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Apple transaction JWS verification failed for user {UserId}.", userId);
                    throw new InvalidOperationException("Failed to verify Apple transaction receipt.", ex);
                }

                finalProductId = verifiedProductId ?? dto.ProductId;
                finalOriginalTransactionId = verifiedOriginalTransactionId ?? dto.OriginalTransactionId!;
                finalTransactionId = verifiedTransactionId ?? dto.TransactionId;
                finalExpiresAtUtc = verifiedExpiresAtUtc ?? dto.ExpiresAtUtc;
                finalAppAccountToken = verifiedAppAccountToken ?? dto.AppAccountToken;
            }
            else
            {
                _logger.LogError("Apple subscription activation failed: provide either TransactionReceipt (legacy) or SignedTransactionJws (StoreKit 2).");
                throw new InvalidOperationException("Provide either TransactionReceipt (legacy) or SignedTransactionJws (StoreKit 2) with OriginalTransactionId and ExpiresAtUtc.");
            }

            // First, try to find existing subscription by originalTransactionId (may have been created by webhook)
            var existingByTransactionId = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us =>
                    us.Provider == SubscriptionProvider.Apple &&
                    us.OriginalTransactionId == finalOriginalTransactionId);

            if (existingByTransactionId != null)
            {
                _logger.LogInformation("Found existing subscription by OriginalTransactionId={OriginalTransactionId}. Updating with userId={UserId}.", 
                    finalOriginalTransactionId, userId);
                
                // Link the subscription to the user if it wasn't already linked
                if (existingByTransactionId.UserId == Guid.Empty)
                {
                    existingByTransactionId.UserId = userId;
                    _logger.LogInformation("Linked subscription to user: UserId={UserId}", userId);
                }
                else if (existingByTransactionId.UserId != userId)
                {
                    _logger.LogWarning("Subscription already linked to different user: ExistingUserId={ExistingUserId}, NewUserId={NewUserId}. " +
                        "This may indicate a transaction ID conflict.", existingByTransactionId.UserId, userId);
                }

                // Cancel any other active subscriptions for this user (single active at a time)
                var otherActive = await _dbContext.UserSubscriptions
                    .Where(us => us.UserId == userId && 
                                us.Status == SubscriptionStatus.Active && 
                                us.Id != existingByTransactionId.Id)
                    .ToListAsync();
                
                foreach (var other in otherActive)
                {
                    other.Status = SubscriptionStatus.Cancelled;
                    other.EndDate = DateTime.UtcNow;
                    _logger.LogInformation("Cancelled other active subscription: SubscriptionId={SubscriptionId}", other.Id);
                }

                // Update subscription with verified data from transaction JWS
                existingByTransactionId.PlanId = finalProductId;
                existingByTransactionId.ProductId = finalProductId;
                existingByTransactionId.LatestTransactionId = finalTransactionId;
                existingByTransactionId.ExpiresAtUtc = finalExpiresAtUtc;
                existingByTransactionId.EndDate = finalExpiresAtUtc;
                existingByTransactionId.Status = SubscriptionStatus.Active;
                existingByTransactionId.AutoRenewEnabled = true;
                existingByTransactionId.Environment = environment;
                existingByTransactionId.AppAccountToken = finalAppAccountToken ?? existingByTransactionId.AppAccountToken;
                existingByTransactionId.RawTransactionPayload = dto.SignedTransactionJws ?? dto.TransactionReceipt ?? existingByTransactionId.RawTransactionPayload;
                existingByTransactionId.RawRenewalPayload = dto.SignedRenewalInfoJws ?? existingByTransactionId.RawRenewalPayload;

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Apple subscription updated for user {UserId} with product {ProductId}", userId, finalProductId);
                return await ToUserSubscriptionDtoAsync(existingByTransactionId);
            }

            // No existing subscription found by originalTransactionId - cancel any existing active subscription for this user
            var existing = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == SubscriptionStatus.Active);
            if (existing != null)
            {
                existing.Status = SubscriptionStatus.Cancelled;
                existing.EndDate = DateTime.UtcNow;
                _logger.LogInformation("Cancelled existing active subscription: SubscriptionId={SubscriptionId}", existing.Id);
            }

            // Create new subscription with verified data
            var now = DateTime.UtcNow;
            var userSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = finalProductId, // Use verified productId from JWS
                StartDate = now,
                EndDate = finalExpiresAtUtc, // Use verified expiry
                Status = SubscriptionStatus.Active,
                // Provider fields
                Provider = SubscriptionProvider.Apple,
                ProductId = finalProductId,
                OriginalTransactionId = finalOriginalTransactionId,
                LatestTransactionId = finalTransactionId,
                ExpiresAtUtc = finalExpiresAtUtc,
                AutoRenewEnabled = true,
                Environment = environment,
                AppAccountToken = finalAppAccountToken,
                RawTransactionPayload = dto.SignedTransactionJws ?? dto.TransactionReceipt,
                RawRenewalPayload = dto.SignedRenewalInfoJws
            };

            await _dbContext.UserSubscriptions.AddAsync(userSubscription);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Apple subscription activated for user {UserId} with product {ProductId}", userId, finalProductId);
            return await ToUserSubscriptionDtoAsync(userSubscription);
        }

        public Task<UserSubscriptionDto> VerifyAppleReceiptAsync(Guid userId, AppleSubscribeRequest dto)
        {
            // For now, reuse the same verification + upsert logic as ActivateAppleSubscriptionAsync.
            // JWS (StoreKit 2) path uses SignedDataVerifier; legacy receipt path currently trusts client fields.
            return ActivateAppleSubscriptionAsync(userId, dto);
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
                var notificationSubtype = outerRoot.TryGetProperty("subtype", out var subtypeEl)
                    ? subtypeEl.GetString() ?? string.Empty
                    : string.Empty;
                var environment = outerRoot.TryGetProperty("environment", out var envEl)
                    ? envEl.GetString() ?? "sandbox"
                    : "sandbox";

                _logger.LogInformation("Processing Apple notification V2: Type={NotificationType}, Subtype={Subtype}, Environment={Environment}", 
                    notificationType, notificationSubtype ?? "none", environment);

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

                // Extract appAccountToken if available (used to identify user)
                Guid? appAccountToken = null;
                if (txRoot.TryGetProperty("appAccountToken", out var appTokenEl) && appTokenEl.ValueKind == JsonValueKind.String)
                {
                    var appTokenStr = appTokenEl.GetString();
                    if (!string.IsNullOrWhiteSpace(appTokenStr) && Guid.TryParse(appTokenStr, out var parsedToken))
                    {
                        appAccountToken = parsedToken;
                    }
                }

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
                    "Extracted transaction details: OriginalTransactionId={OriginalTransactionId}, TransactionId={TransactionId}, ProductId={ProductId}, ExpiresAtUtc={ExpiresAtUtc}, AppAccountToken={AppAccountToken}",
                    originalTransactionId,
                    transactionId ?? "null",
                    productId ?? "null",
                    expiresAtUtc?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "null",
                    appAccountToken?.ToString() ?? "null");

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
                    // Resolve user strictly via appAccountToken mapping (issued by our API before purchase)
                    Guid? userId = null;
                    if (appAccountToken.HasValue)
                    {
                        _logger.LogInformation("No subscription found. Attempting to find user by AppAccountToken={AppAccountToken}...", appAccountToken.Value);

                        var mapping = await _dbContext.AppleAppAccountTokens
                            .Where(m => m.AppAccountToken == appAccountToken.Value && m.IsActive)
                            .OrderByDescending(m => m.Created)
                            .FirstOrDefaultAsync();

                        if (mapping != null)
                        {
                            userId = mapping.UserId;
                            mapping.IsActive = false;
                            _logger.LogInformation("Found user via AppleAppAccountToken mapping: UserId={UserId}", userId.Value);
                        }
                        else
                        {
                            _logger.LogWarning("No user mapping found via AppAccountToken. Subscription will be created without userId.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No AppAccountToken in webhook payload. Subscription will be created without userId.");
                    }

                    _logger.LogInformation("Creating new UserSubscription for OriginalTransactionId={OriginalTransactionId} (UserId={UserId})...", 
                        originalTransactionId, userId?.ToString() ?? "Guid.Empty");
                    subscription = new UserSubscription
                    {
                        UserId = userId ?? Guid.Empty, // Will be linked when user calls subscribe endpoint if not found via appAccountToken
                        PlanId = productId ?? string.Empty, // Use productId from Apple as PlanId
                        Provider = SubscriptionProvider.Apple,
                        ProductId = productId ?? string.Empty,
                        OriginalTransactionId = originalTransactionId,
                        LatestTransactionId = transactionId ?? string.Empty,
                        AppAccountToken = appAccountToken,
                        StartDate = DateTime.UtcNow,
                        Environment = environment,
                        ExpiresAtUtc = expiresAtUtc
                    };
                    await _dbContext.UserSubscriptions.AddAsync(subscription);
                }
                else
                {
                    _logger.LogInformation("Found existing subscription: SubscriptionId={SubscriptionId}, UserId={UserId}, CurrentStatus={Status}, CurrentProductId={ProductId}",
                        subscription.Id, subscription.UserId, subscription.Status, subscription.ProductId);
                    
                    // Update appAccountToken if provided and not already set
                    if (appAccountToken.HasValue && !subscription.AppAccountToken.HasValue)
                    {
                        subscription.AppAccountToken = appAccountToken.Value;
                        _logger.LogInformation("Updated AppAccountToken for existing subscription: {AppAccountToken}", appAccountToken.Value);
                    }
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

                // 4) Map notificationType (V2) -> subscription status
                var previousStatus = subscription.Status;
                var subtypeUpper = notificationSubtype.ToUpperInvariant();
                _logger.LogInformation("Processing notification V2: Type={NotificationType}, Subtype={Subtype} (previous status: {PreviousStatus})", 
                    notificationType, subtypeUpper ?? "none", previousStatus);

                switch (notificationType.ToUpperInvariant())
                {
                    // SUBSCRIBED: Customer subscribed to auto-renewable subscription
                    // Subtypes: INITIAL_BUY (first purchase), RESUBSCRIBE (resubscribed or Family Sharing)
                    case "SUBSCRIBED":
                        if (subtypeUpper == "INITIAL_BUY")
                        {
                            _logger.LogInformation("Processing SUBSCRIBED/INITIAL_BUY: Customer purchased subscription for the first time.");
                        }
                        else if (subtypeUpper == "RESUBSCRIBE")
                        {
                            _logger.LogInformation("Processing SUBSCRIBED/RESUBSCRIBE: Customer resubscribed or received access via Family Sharing.");
                        }
                        else
                        {
                            _logger.LogInformation("Processing SUBSCRIBED: Customer subscribed to subscription.");
                        }
                        subscription.Status = SubscriptionStatus.Active;
                        subscription.EndDate = expiresAtUtc;
                        break;

                    // DID_RENEW: Subscription successfully renewed
                    // Subtypes: BILLING_RECOVERY (expired subscription recovered), empty (normal auto-renewal)
                    case "DID_RENEW":
                        if (subtypeUpper == "BILLING_RECOVERY")
                        {
                            _logger.LogInformation("Processing DID_RENEW/BILLING_RECOVERY: Expired subscription that failed to renew has successfully renewed.");
                        }
                        else
                        {
                            _logger.LogInformation("Processing DID_RENEW: Subscription successfully auto-renewed for new transaction period.");
                        }
                        subscription.Status = SubscriptionStatus.Active;
                        subscription.EndDate = expiresAtUtc;
                        break;

                    // EXPIRED: Subscription expired
                    // Subtypes: VOLUNTARY (customer turned off renewal), BILLING_RETRY (billing retry period ended),
                    //           PRICE_INCREASE (didn't consent to price increase), PRODUCT_NOT_FOR_SALE (product unavailable)
                    case "EXPIRED":
                        if (subtypeUpper == "VOLUNTARY")
                        {
                            _logger.LogInformation("Processing EXPIRED/VOLUNTARY: Subscription expired after customer turned off renewal.");
                        }
                        else if (subtypeUpper == "BILLING_RETRY")
                        {
                            _logger.LogInformation("Processing EXPIRED/BILLING_RETRY: Subscription expired because billing retry period ended.");
                        }
                        else if (subtypeUpper == "PRICE_INCREASE")
                        {
                            _logger.LogInformation("Processing EXPIRED/PRICE_INCREASE: Subscription expired because customer didn't consent to price increase.");
                        }
                        else if (subtypeUpper == "PRODUCT_NOT_FOR_SALE")
                        {
                            _logger.LogInformation("Processing EXPIRED/PRODUCT_NOT_FOR_SALE: Subscription expired because product wasn't available for purchase.");
                        }
                        else
                        {
                            _logger.LogInformation("Processing EXPIRED: Subscription expired.");
                        }
                        subscription.Status = SubscriptionStatus.Expired;
                        subscription.EndDate = expiresAtUtc ?? DateTime.UtcNow;
                        break;

                    // DID_FAIL_TO_RENEW: Subscription failed to renew due to billing issue
                    // Subtypes: GRACE_PERIOD (in grace period - continue service), empty (not in grace period - stop service)
                    case "DID_FAIL_TO_RENEW":
                        if (subtypeUpper == "GRACE_PERIOD")
                        {
                            _logger.LogInformation("Processing DID_FAIL_TO_RENEW/GRACE_PERIOD: Subscription in billing grace period - continue providing service.");
                            subscription.Status = SubscriptionStatus.InGrace; // Keep service active during grace period
                        }
                        else
                        {
                            _logger.LogInformation("Processing DID_FAIL_TO_RENEW: Subscription failed to renew - not in grace period.");
                            subscription.Status = SubscriptionStatus.BillingRetry;
                        }
                        if (expiresAtUtc.HasValue)
                        {
                            subscription.EndDate = expiresAtUtc.Value; // May be grace period expiry
                        }
                        break;

                    // GRACE_PERIOD_EXPIRED: Billing grace period ended without renewal
                    case "GRACE_PERIOD_EXPIRED":
                        _logger.LogInformation("Processing GRACE_PERIOD_EXPIRED: Billing grace period ended without renewing - turn off access.");
                        subscription.Status = SubscriptionStatus.Expired;
                        subscription.EndDate = expiresAtUtc ?? DateTime.UtcNow;
                        break;

                    // DID_CHANGE_RENEWAL_PREF: Customer changed subscription plan
                    // Subtypes: UPGRADE (immediate, prorated refund), DOWNGRADE (effective at next renewal), empty (canceled downgrade)
                    case "DID_CHANGE_RENEWAL_PREF":
                        if (subtypeUpper == "UPGRADE")
                        {
                            _logger.LogInformation("Processing DID_CHANGE_RENEWAL_PREF/UPGRADE: Customer upgraded subscription - effective immediately with prorated refund.");
                            // Upgrade takes effect immediately - update plan/productId if available
                            if (!string.IsNullOrWhiteSpace(productId))
                            {
                                subscription.PlanId = productId;
                                subscription.ProductId = productId;
                            }
                            subscription.Status = SubscriptionStatus.Active;
                            subscription.EndDate = expiresAtUtc;
                        }
                        else if (subtypeUpper == "DOWNGRADE")
                        {
                            _logger.LogInformation("Processing DID_CHANGE_RENEWAL_PREF/DOWNGRADE: Customer downgraded subscription - effective at next renewal.");
                            // Downgrade takes effect at next renewal - current plan remains active
                            // Note: Check auto_renew_product_id in pending_renewal_info for new product
                            if (expiresAtUtc.HasValue)
                            {
                                subscription.EndDate = expiresAtUtc.Value;
                            }
                            // Status remains unchanged - current plan active until renewal
                        }
                        else
                        {
                            _logger.LogInformation("Processing DID_CHANGE_RENEWAL_PREF: Customer changed renewal preference back to current subscription (canceled downgrade).");
                            // Customer canceled a downgrade - no change needed
                        }
                        break;

                    // DID_CHANGE_RENEWAL_STATUS: Change in subscription renewal status
                    // Subtypes: AUTO_RENEW_ENABLED (reenabled), AUTO_RENEW_DISABLED (turned off)
                    // When user cancels during free trial: we only set AutoRenewEnabled = false.
                    // Status stays Active and EndDate unchanged → user keeps access until trial/subscription end (Apple behavior).
                    case "DID_CHANGE_RENEWAL_STATUS":
                        if (subtypeUpper == "AUTO_RENEW_ENABLED")
                        {
                            _logger.LogInformation("Processing DID_CHANGE_RENEWAL_STATUS/AUTO_RENEW_ENABLED: Customer reenabled subscription auto-renewal.");
                            subscription.AutoRenewEnabled = true;
                        }
                        else if (subtypeUpper == "AUTO_RENEW_DISABLED")
                        {
                            _logger.LogInformation("Processing DID_CHANGE_RENEWAL_STATUS/AUTO_RENEW_DISABLED: Customer turned off auto-renewal (e.g. cancelled during trial). Access until EndDate/ExpiresAtUtc.");
                            subscription.AutoRenewEnabled = false;
                        }
                        else
                        {
                            _logger.LogInformation("Processing DID_CHANGE_RENEWAL_STATUS: Subscription renewal status changed.");
                            // Use autoRenewStatus from renewal info if available
                            if (autoRenewStatus.HasValue)
                            {
                                subscription.AutoRenewEnabled = autoRenewStatus.Value;
                            }
                        }
                        // Status remains unchanged - subscription continues until expiry
                        break;

                    // REFUND: App Store refunded a transaction
                    case "REFUND":
                        _logger.LogInformation("Processing REFUND: App Store successfully refunded transaction.");
                        subscription.Status = SubscriptionStatus.Cancelled;
                        subscription.EndDate = expiresAtUtc ?? DateTime.UtcNow;
                        break;

                    // REFUND_DECLINED: App Store declined a refund request
                    case "REFUND_DECLINED":
                        _logger.LogInformation("Processing REFUND_DECLINED: App Store declined refund request - subscription remains active.");
                        // No status change - subscription continues
                        break;

                    // REFUND_REVERSED: App Store reversed a previously granted refund
                    case "REFUND_REVERSED":
                        _logger.LogInformation("Processing REFUND_REVERSED: App Store reversed previously granted refund - reinstate content/services.");
                        // Reinstate subscription if it was cancelled due to refund
                        if (subscription.Status == SubscriptionStatus.Cancelled)
                        {
                            subscription.Status = SubscriptionStatus.Active;
                            subscription.EndDate = expiresAtUtc;
                        }
                        break;

                    // REVOKE: Family Sharing entitlement revoked
                    case "REVOKE":
                        _logger.LogInformation("Processing REVOKE: Family Sharing entitlement revoked.");
                        subscription.Status = SubscriptionStatus.Cancelled;
                        subscription.EndDate = expiresAtUtc ?? DateTime.UtcNow;
                        break;

                    // PRICE_INCREASE: System informed customer of price increase
                    // Subtypes: PENDING (customer hasn't responded), ACCEPTED (customer consented or doesn't require consent)
                    case "PRICE_INCREASE":
                        if (subtypeUpper == "PENDING")
                        {
                            _logger.LogInformation("Processing PRICE_INCREASE/PENDING: Customer hasn't responded to price increase requiring consent.");
                        }
                        else if (subtypeUpper == "ACCEPTED")
                        {
                            _logger.LogInformation("Processing PRICE_INCREASE/ACCEPTED: Customer consented to price increase or doesn't require consent.");
                        }
                        else
                        {
                            _logger.LogInformation("Processing PRICE_INCREASE: System informed customer of price increase.");
                        }
                        // Status remains unchanged - informational only
                        break;

                    // OFFER_REDEEMED: Customer redeemed subscription offer
                    // Subtypes: UPGRADE (immediate), DOWNGRADE (next renewal), empty (redeemed for active subscription)
                    case "OFFER_REDEEMED":
                        if (subtypeUpper == "UPGRADE")
                        {
                            _logger.LogInformation("Processing OFFER_REDEEMED/UPGRADE: Customer redeemed offer to upgrade - effective immediately.");
                            if (!string.IsNullOrWhiteSpace(productId))
                            {
                                subscription.PlanId = productId;
                                subscription.ProductId = productId;
                            }
                            subscription.Status = SubscriptionStatus.Active;
                            subscription.EndDate = expiresAtUtc;
                        }
                        else if (subtypeUpper == "DOWNGRADE")
                        {
                            _logger.LogInformation("Processing OFFER_REDEEMED/DOWNGRADE: Customer redeemed offer to downgrade - effective at next renewal.");
                            // Downgrade effective at next renewal - current plan remains active
                        }
                        else
                        {
                            _logger.LogInformation("Processing OFFER_REDEEMED: Customer redeemed offer for active subscription.");
                        }
                        break;

                    // CONSUMPTION_REQUEST: Request for consumption data (for consumables/refund requests)
                    case "CONSUMPTION_REQUEST":
                        _logger.LogWarning("Processing CONSUMPTION_REQUEST: Request for consumption data (for consumables or refund requests).");
                        // This is for consumables or refund requests - may apply to subscriptions for refunds
                        // No status change - handle consumption data response separately
                        break;

                    // ONE_TIME_CHARGE: Customer purchased consumable, non-consumable, or non-renewing subscription
                    case "ONE_TIME_CHARGE":
                        _logger.LogInformation("Processing ONE_TIME_CHARGE: Customer purchased one-time product (not auto-renewable subscription).");
                        // This is for non-subscription products - may not apply to our subscription model
                        // No status change needed
                        break;

                    // EXTERNAL_PURCHASE_TOKEN: External Purchase API notification
                    // Subtypes: CREATED, ACTIVE_TOKEN_REMINDER, UNREPORTED
                    case "EXTERNAL_PURCHASE_TOKEN":
                        _logger.LogInformation("Processing EXTERNAL_PURCHASE_TOKEN: External Purchase API notification (subtype: {Subtype}).", subtypeUpper);
                        // This applies to apps using External Purchase API - may not apply to standard subscriptions
                        break;

                    // RENEWAL_EXTENDED: App Store extended subscription renewal date
                    case "RENEWAL_EXTENDED":
                        _logger.LogInformation("Processing RENEWAL_EXTENDED: App Store extended subscription renewal date.");
                        if (expiresAtUtc.HasValue)
                        {
                            subscription.EndDate = expiresAtUtc.Value;
                            subscription.ExpiresAtUtc = expiresAtUtc.Value;
                        }
                        // Status remains active
                        break;

                    // RENEWAL_EXTENSION: App Store attempting to extend renewal dates
                    // Subtypes: SUMMARY (completed for all subscribers), FAILURE (didn't succeed for specific subscription)
                    case "RENEWAL_EXTENSION":
                        if (subtypeUpper == "SUMMARY")
                        {
                            _logger.LogInformation("Processing RENEWAL_EXTENSION/SUMMARY: Completed extending renewal dates for all eligible subscribers.");
                        }
                        else if (subtypeUpper == "FAILURE")
                        {
                            _logger.LogWarning("Processing RENEWAL_EXTENSION/FAILURE: Renewal date extension didn't succeed for this subscription.");
                        }
                        else
                        {
                            _logger.LogInformation("Processing RENEWAL_EXTENSION: App Store attempting to extend renewal dates.");
                        }
                        // Check responseBodyV2DecodedPayload for details
                        break;

                    // METADATA_UPDATE: Changed subscription metadata via Advanced Commerce API
                    case "METADATA_UPDATE":
                        _logger.LogInformation("Processing METADATA_UPDATE: Subscription metadata changed via Advanced Commerce API.");
                        // This applies to Advanced Commerce API - no status change
                        break;

                    // MIGRATION: Migrated subscription to Advanced Commerce API
                    case "MIGRATION":
                        _logger.LogInformation("Processing MIGRATION: Subscription migrated to Advanced Commerce API.");
                        // This applies to Advanced Commerce API - no status change
                        break;

                    // PRICE_CHANGE: Changed subscription price via Advanced Commerce API
                    case "PRICE_CHANGE":
                        _logger.LogInformation("Processing PRICE_CHANGE: Subscription price changed via Advanced Commerce API.");
                        // This applies to Advanced Commerce API - no status change
                        break;

                    // RESCIND_CONSENT: Parent/guardian withdrew consent for child's app usage
                    case "RESCIND_CONSENT":
                        _logger.LogInformation("Processing RESCIND_CONSENT: Parent/guardian withdrew consent for child's app usage.");
                        subscription.Status = SubscriptionStatus.Cancelled;
                        subscription.EndDate = expiresAtUtc ?? DateTime.UtcNow;
                        break;

                    // TEST: Test notification requested via Request a Test Notification endpoint
                    case "TEST":
                        _logger.LogInformation("Processing TEST: Test notification received - server is receiving notifications correctly.");
                        // No status change - this is just a test
                        break;

                    default:
                        _logger.LogWarning("Unhandled Apple notification V2 type: {Type} (subtype: {Subtype}). Subscription status unchanged.", 
                            notificationType, subtypeUpper ?? "none");
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

        public async Task<Guid> CreateAppleAppAccountTokenAsync(Guid userId)
        {
            // Reuse existing active token for this user if one exists
            var existing = await _dbContext.AppleAppAccountTokens
                .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive);

            if (existing != null)
            {
                _logger.LogInformation("Reusing existing Apple appAccountToken mapping: UserId={UserId}, AppAccountToken={AppAccountToken}", userId, existing.AppAccountToken);
                return existing.AppAccountToken;
            }

            var token = Guid.NewGuid();
            var mapping = new AppleAppAccountToken
            {
                UserId = userId,
                AppAccountToken = token,
                IsActive = true
            };
            await _dbContext.AppleAppAccountTokens.AddAsync(mapping);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Created Apple appAccountToken mapping: UserId={UserId}, AppAccountToken={AppAccountToken}", userId, token);
            return token;
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

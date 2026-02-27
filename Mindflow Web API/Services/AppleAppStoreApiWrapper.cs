using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Exceptions;
using Mimo.AppStoreServerLibrary.Models;

namespace Mindflow_Web_API.Services;

/// <summary>
/// Wraps Mimo App Store Server API clients (Production + Sandbox) so we can resolve a transaction
/// without knowing the environment. Uses only Mimo.AppStoreServerLibrary (no direct verifyReceipt calls).
/// Initializes <see cref="AppStoreServerApiClient" /> instances from IConfiguration instead of Program.cs.
/// </summary>
public class AppleAppStoreApiWrapper
{
    private readonly ILogger<AppleAppStoreApiWrapper> _logger;
    private readonly AppStoreServerApiClient? _production;
    private readonly AppStoreServerApiClient? _sandbox;
    private readonly AppStoreEnvironment _defaultEnvironment;

    public AppleAppStoreApiWrapper(IConfiguration configuration, ILogger<AppleAppStoreApiWrapper> logger)
    {
        _logger = logger;

        var appleEnvValue = configuration["Apple:Environment"];
        _defaultEnvironment = string.Equals(appleEnvValue, "Sandbox", StringComparison.OrdinalIgnoreCase)
            ? AppStoreEnvironment.Sandbox
            : AppStoreEnvironment.Production;

        var appleSigningKeyRaw = configuration["Apple:SigningKey"];
        _logger.LogInformation("Apple:SigningKey raw configuration value length={Len}", appleSigningKeyRaw?.Length ?? 0);
        var appleSigningKey = ResolveAppleSigningKeyPem(appleSigningKeyRaw);


        var appleKeyId = configuration["Apple:KeyId"];
        var appleIssuerId = configuration["Apple:IssuerId"];
        var appleBundleId = configuration["Apple:BundleId"];

        if (!string.IsNullOrWhiteSpace(appleSigningKey) &&
            !string.IsNullOrWhiteSpace(appleKeyId) &&
            !string.IsNullOrWhiteSpace(appleIssuerId) &&
            !string.IsNullOrWhiteSpace(appleBundleId))
        {
            _production = new AppStoreServerApiClient(appleSigningKey, appleKeyId, appleIssuerId, appleBundleId, AppStoreEnvironment.Production);
            _sandbox = new AppStoreServerApiClient(appleSigningKey, appleKeyId, appleIssuerId, appleBundleId, AppStoreEnvironment.Sandbox);
            _logger.LogInformation("AppleAppStoreApiWrapper initialized with production and sandbox clients. Default environment: {DefaultEnv}", _defaultEnvironment);
        }
        else
        {
            _logger.LogWarning("Apple App Store API is not fully configured. Ensure Apple:SigningKey, Apple:KeyId, Apple:IssuerId, and Apple:BundleId are set.");
        }
    }

    /// <summary>
    /// Gets transaction info for the given transaction ID.
    /// Uses the environment from configuration as the primary source:
    /// - If Apple:Environment is Sandbox, tries sandbox first then production.
    /// - Otherwise, tries production first then sandbox.
    /// </summary>
    public async Task<TransactionInfoResponse?> GetTransactionInfoAsync(string transactionId)
    {
        if (_production == null || _sandbox == null)
            throw new InvalidOperationException(
                "Apple App Store API is not configured. For legacy receipt verification add Apple:SigningKey, Apple:KeyId, Apple:IssuerId (and Apple:BundleId if different).");

        // If config says Sandbox, prefer sandbox first
        if (_defaultEnvironment == AppStoreEnvironment.Sandbox)
        {
            try
            {
                var sandboxResponse = await _sandbox.GetTransactionInfo(transactionId);
                if (sandboxResponse != null && !string.IsNullOrWhiteSpace(sandboxResponse.SignedTransactionInfo))
                    return sandboxResponse;
            }
            catch (ApiException)
            {
                // Not found or other API error in sandbox; fall back to production
            }

            return await _production.GetTransactionInfo(transactionId);
        }

        // Default: prefer production first
        try
        {
            var response = await _production.GetTransactionInfo(transactionId);
            if (response != null && !string.IsNullOrWhiteSpace(response.SignedTransactionInfo))
                return response;
        }
        catch (ApiException)
        {
            // Transaction not in production (e.g. 404) or other API error; try sandbox
        }

        return await _sandbox.GetTransactionInfo(transactionId);
    }

    private string? ResolveAppleSigningKeyPem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase))
            return value;

        // Value may be raw base64 (e.g. from appsettings). PEM requires headers for ImportFromPem().
        var base64 = value.Replace("\r", "").Replace("\n", "").Trim();
        if (IsLikelyBase64Key(base64))
        {
            _logger.LogInformation("Apple:SigningKey treated as raw base64; wrapping into PEM.");
            return "-----BEGIN PRIVATE KEY-----\n" + base64 + "\n-----END PRIVATE KEY-----";
        }

        _logger.LogError("Apple:SigningKey is neither a PEM block nor a valid base64 key string.");
        throw new InvalidOperationException("Apple:SigningKey must be either full PEM content or raw base64 for the private key.");
    }

    private static bool IsLikelyBase64Key(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        // Base64 should only contain A–Z, a–z, 0–9, +, /, =
        foreach (var c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '='))
                return false;
        }
        // Length should be divisible by 4 for standard base64
        return s.Length % 4 == 0;
    }
}

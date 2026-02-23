using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Exceptions;
using Mimo.AppStoreServerLibrary.Models;

namespace Mindflow_Web_API.Services;

/// <summary>
/// Wraps Mimo App Store Server API clients (Production + Sandbox) so we can resolve a transaction
/// without knowing the environment. Uses only Mimo.AppStoreServerLibrary (no direct verifyReceipt calls).
/// </summary>
public class AppleAppStoreApiWrapper
{
    private readonly AppStoreServerApiClient? _production;
    private readonly AppStoreServerApiClient? _sandbox;
    private readonly AppStoreEnvironment _defaultEnvironment;

    public AppleAppStoreApiWrapper(
        AppStoreServerApiClient? production,
        AppStoreServerApiClient? sandbox,
        AppStoreEnvironment defaultEnvironment)
    {
        _production = production;
        _sandbox = sandbox;
        _defaultEnvironment = defaultEnvironment;
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
}

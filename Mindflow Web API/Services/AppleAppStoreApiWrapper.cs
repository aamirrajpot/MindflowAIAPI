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

    public AppleAppStoreApiWrapper(AppStoreServerApiClient? production, AppStoreServerApiClient? sandbox)
    {
        _production = production;
        _sandbox = sandbox;
    }

    /// <summary>
    /// Gets transaction info for the given transaction ID. Tries Production first, then Sandbox (e.g. for sandbox receipts).
    /// </summary>
    public async Task<TransactionInfoResponse?> GetTransactionInfoAsync(string transactionId)
    {
        if (_production == null || _sandbox == null)
            throw new InvalidOperationException(
                "Apple App Store API is not configured. For legacy receipt verification add Apple:SigningKey, Apple:KeyId, Apple:IssuerId (and Apple:BundleId if different).");

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

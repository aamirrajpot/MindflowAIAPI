using System;

namespace Mindflow_Web_API.DTOs
{
    // Customer DTOs
    public record CreateCustomerResource(
        string Email,
        string Name
    );

    public record CustomerResource(
        string Id,
        string Email,
        string Name
    );

    // Removed card details and token usage; PaymentSheet will handle payment methods client-side

    // Charge DTOs
    public record CreateChargeResource(
        string Currency,
        long Amount,
        string ReceiptEmail,
        string CustomerId,
        string Description
    );

    public record ChargeResource(
        string Id,
        string Currency,
        long Amount,
        string CustomerId,
        string ReceiptEmail,
        string Description,
        DateTime Created
    );

    // Payment Sheet DTOs
    public record CreatePaymentSheetResource(
        decimal Amount,
        string Currency,
        string Email,
        string Name,
        string? CustomerId = null,
        Guid? PlanId = null
    );

    public record PaymentSheetResource(
        string PaymentIntentClientSecret,
        string CustomerId,
        string EphemeralKeySecret,
        string PublishableKey
    );

    // Test Webhook DTO
    public record TestWebhookDto(
        Guid? UserId = null,
        Guid? PlanId = null,
        string? PaymentIntentId = null,
        long? Amount = null,
        string? Currency = null,
        string? Description = null
    );
}

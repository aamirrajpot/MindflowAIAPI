using Mindflow_Web_API.Models;
using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.DTOs
{
    // Payment Card DTOs
    public record PaymentCardDto(
        Guid Id,
        Guid UserId,
        string CardNumber,
        string CardholderName,
        string ExpiryMonth,
        string ExpiryYear,
        string CardType,
        bool IsDefault,
        bool IsActive,
        string? LastFourDigits
    );

    public record CreatePaymentCardDto(
        string CardNumber,
        string CardholderName,
        string ExpiryMonth,
        string ExpiryYear,
        string CardType,
        bool IsDefault = false
    );

    public record UpdatePaymentCardDto(
        string? CardholderName,
        string? ExpiryMonth,
        string? ExpiryYear,
        bool? IsDefault,
        bool? IsActive
    );

    // Payment History DTOs
    public record PaymentHistoryDto(
        Guid Id,
        Guid UserId,
        Guid? PaymentCardId,
        Guid? SubscriptionPlanId,
        decimal Amount,
        string Currency,
        string Description,
        PaymentStatus Status,
        string? TransactionId,
        string? PaymentMethod,
        string? FailureReason,
        DateTime TransactionDate,
        PaymentCardDto? PaymentCard,
        SubscriptionPlanDto? SubscriptionPlan
    );

    public record CreatePaymentHistoryDto(
        Guid? PaymentCardId,
        Guid? SubscriptionPlanId,
        decimal Amount,
        string Currency,
        string Description,
        PaymentStatus Status,
        string? TransactionId = null,
        string? PaymentMethod = null,
        string? FailureReason = null
    );

    public record UpdatePaymentHistoryDto(
        PaymentStatus? Status,
        string? TransactionId,
        string? FailureReason
    );

    // Wallet Overview DTOs
    public record WalletOverviewDto(
        List<PaymentCardDto> PaymentCards,
        List<PaymentHistoryDto> PaymentHistory,
        PaymentCardDto? DefaultCard
    );

    // Payment Processing DTOs
    public record ProcessPaymentDto(
        Guid? PaymentCardId,
        Guid SubscriptionPlanId,
        string? CardNumber = null, // For one-time payments without saving card
        string? CardholderName = null,
        string? ExpiryMonth = null,
        string? ExpiryYear = null,
        string? CardType = null,
        bool SaveCard = false
    );

    public record PaymentResultDto(
        bool Success,
        string Message,
        string? TransactionId,
        PaymentHistoryDto? PaymentRecord
    );
}

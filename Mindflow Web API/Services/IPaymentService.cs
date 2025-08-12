using Mindflow_Web_API.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public interface IPaymentService
    {
        // Payment Cards
        Task<PaymentCardDto> AddPaymentCardAsync(Guid userId, CreatePaymentCardDto dto);
        Task<PaymentCardDto?> GetPaymentCardByIdAsync(Guid userId, Guid cardId);
        Task<IEnumerable<PaymentCardDto>> GetUserPaymentCardsAsync(Guid userId);
        Task<PaymentCardDto?> UpdatePaymentCardAsync(Guid userId, Guid cardId, UpdatePaymentCardDto dto);
        Task<bool> DeletePaymentCardAsync(Guid userId, Guid cardId);
        Task<bool> SetDefaultCardAsync(Guid userId, Guid cardId);

        // Payment History
        Task<PaymentHistoryDto> CreatePaymentRecordAsync(Guid userId, CreatePaymentHistoryDto dto);
        Task<PaymentHistoryDto?> GetPaymentHistoryByIdAsync(Guid userId, Guid paymentId);
        Task<IEnumerable<PaymentHistoryDto>> GetUserPaymentHistoryAsync(Guid userId);
        Task<PaymentHistoryDto?> UpdatePaymentHistoryAsync(Guid userId, Guid paymentId, UpdatePaymentHistoryDto dto);

        // Wallet Overview
        Task<WalletOverviewDto> GetWalletOverviewAsync(Guid userId);

        // Payment Processing (simulated for now)
        Task<PaymentResultDto> ProcessPaymentAsync(Guid userId, ProcessPaymentDto dto);
    }
}

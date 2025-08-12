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
    public class PaymentService : IPaymentService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(MindflowDbContext dbContext, ILogger<PaymentService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // Payment Cards
        public async Task<PaymentCardDto> AddPaymentCardAsync(Guid userId, CreatePaymentCardDto dto)
        {
            // If this card is set as default, unset other default cards
            if (dto.IsDefault)
            {
                var existingDefaultCards = await _dbContext.PaymentCards
                    .Where(pc => pc.UserId == userId && pc.IsDefault)
                    .ToListAsync();

                foreach (var card in existingDefaultCards)
                {
                    card.IsDefault = false;
                }
            }

            var paymentCard = new PaymentCard
            {
                UserId = userId,
                CardNumber = MaskCardNumber(dto.CardNumber), // Store masked version for security
                CardholderName = dto.CardholderName,
                ExpiryMonth = dto.ExpiryMonth,
                ExpiryYear = dto.ExpiryYear,
                CardType = dto.CardType,
                IsDefault = dto.IsDefault,
                IsActive = true,
                LastFourDigits = GetLastFourDigits(dto.CardNumber)
            };

            await _dbContext.PaymentCards.AddAsync(paymentCard);
            await _dbContext.SaveChangesAsync();

            return ToPaymentCardDto(paymentCard);
        }

        public async Task<PaymentCardDto?> GetPaymentCardByIdAsync(Guid userId, Guid cardId)
        {
            var paymentCard = await _dbContext.PaymentCards
                .FirstOrDefaultAsync(pc => pc.Id == cardId && pc.UserId == userId);

            return paymentCard == null ? null : ToPaymentCardDto(paymentCard);
        }

        public async Task<IEnumerable<PaymentCardDto>> GetUserPaymentCardsAsync(Guid userId)
        {
            var paymentCards = await _dbContext.PaymentCards
                .Where(pc => pc.UserId == userId && pc.IsActive)
                .OrderByDescending(pc => pc.IsDefault)
                .ThenBy(pc => pc.Created)
                .ToListAsync();

            return paymentCards.Select(ToPaymentCardDto);
        }

        public async Task<PaymentCardDto?> UpdatePaymentCardAsync(Guid userId, Guid cardId, UpdatePaymentCardDto dto)
        {
            var paymentCard = await _dbContext.PaymentCards
                .FirstOrDefaultAsync(pc => pc.Id == cardId && pc.UserId == userId);

            if (paymentCard == null) return null;

            if (dto.CardholderName != null) paymentCard.CardholderName = dto.CardholderName;
            if (dto.ExpiryMonth != null) paymentCard.ExpiryMonth = dto.ExpiryMonth;
            if (dto.ExpiryYear != null) paymentCard.ExpiryYear = dto.ExpiryYear;
            if (dto.IsActive.HasValue) paymentCard.IsActive = dto.IsActive.Value;

            // Handle default card setting
            if (dto.IsDefault.HasValue && dto.IsDefault.Value)
            {
                // Unset other default cards
                var existingDefaultCards = await _dbContext.PaymentCards
                    .Where(pc => pc.UserId == userId && pc.IsDefault && pc.Id != cardId)
                    .ToListAsync();

                foreach (var card in existingDefaultCards)
                {
                    card.IsDefault = false;
                }

                paymentCard.IsDefault = true;
            }

            await _dbContext.SaveChangesAsync();
            return ToPaymentCardDto(paymentCard);
        }

        public async Task<bool> DeletePaymentCardAsync(Guid userId, Guid cardId)
        {
            var paymentCard = await _dbContext.PaymentCards
                .FirstOrDefaultAsync(pc => pc.Id == cardId && pc.UserId == userId);

            if (paymentCard == null) return false;

            _dbContext.PaymentCards.Remove(paymentCard);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetDefaultCardAsync(Guid userId, Guid cardId)
        {
            var paymentCard = await _dbContext.PaymentCards
                .FirstOrDefaultAsync(pc => pc.Id == cardId && pc.UserId == userId);

            if (paymentCard == null) return false;

            // Unset other default cards
            var existingDefaultCards = await _dbContext.PaymentCards
                .Where(pc => pc.UserId == userId && pc.IsDefault)
                .ToListAsync();

            foreach (var card in existingDefaultCards)
            {
                card.IsDefault = false;
            }

            paymentCard.IsDefault = true;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // Payment History
        public async Task<PaymentHistoryDto> CreatePaymentRecordAsync(Guid userId, CreatePaymentHistoryDto dto)
        {
            var paymentHistory = new PaymentHistory
            {
                UserId = userId,
                PaymentCardId = dto.PaymentCardId,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                Amount = dto.Amount,
                Currency = dto.Currency,
                Description = dto.Description,
                Status = dto.Status,
                TransactionId = dto.TransactionId,
                PaymentMethod = dto.PaymentMethod,
                FailureReason = dto.FailureReason,
                TransactionDate = DateTime.UtcNow
            };

            await _dbContext.PaymentHistory.AddAsync(paymentHistory);
            await _dbContext.SaveChangesAsync();

            return await ToPaymentHistoryDtoAsync(paymentHistory);
        }

        public async Task<PaymentHistoryDto?> GetPaymentHistoryByIdAsync(Guid userId, Guid paymentId)
        {
            var paymentHistory = await _dbContext.PaymentHistory
                .Include(ph => ph.PaymentCard)
                .Include(ph => ph.SubscriptionPlan)
                .FirstOrDefaultAsync(ph => ph.Id == paymentId && ph.UserId == userId);

            return paymentHistory == null ? null : await ToPaymentHistoryDtoAsync(paymentHistory);
        }

        public async Task<IEnumerable<PaymentHistoryDto>> GetUserPaymentHistoryAsync(Guid userId)
        {
            var paymentHistory = await _dbContext.PaymentHistory
                .Include(ph => ph.PaymentCard)
                .Include(ph => ph.SubscriptionPlan)
                .Where(ph => ph.UserId == userId)
                .OrderByDescending(ph => ph.TransactionDate)
                .ToListAsync();

            var paymentHistoryDtos = new List<PaymentHistoryDto>();
            foreach (var payment in paymentHistory)
            {
                paymentHistoryDtos.Add(await ToPaymentHistoryDtoAsync(payment));
            }

            return paymentHistoryDtos;
        }

        public async Task<PaymentHistoryDto?> UpdatePaymentHistoryAsync(Guid userId, Guid paymentId, UpdatePaymentHistoryDto dto)
        {
            var paymentHistory = await _dbContext.PaymentHistory
                .FirstOrDefaultAsync(ph => ph.Id == paymentId && ph.UserId == userId);

            if (paymentHistory == null) return null;

            if (dto.Status.HasValue) paymentHistory.Status = dto.Status.Value;
            if (dto.TransactionId != null) paymentHistory.TransactionId = dto.TransactionId;
            if (dto.FailureReason != null) paymentHistory.FailureReason = dto.FailureReason;

            await _dbContext.SaveChangesAsync();
            return await ToPaymentHistoryDtoAsync(paymentHistory);
        }

        // Wallet Overview
        public async Task<WalletOverviewDto> GetWalletOverviewAsync(Guid userId)
        {
            var paymentCards = await GetUserPaymentCardsAsync(userId);
            var paymentHistory = await GetUserPaymentHistoryAsync(userId);
            var defaultCard = paymentCards.FirstOrDefault(pc => pc.IsDefault);

            return new WalletOverviewDto(
                paymentCards.ToList(),
                paymentHistory.ToList(),
                defaultCard
            );
        }

        // Payment Processing (simulated for now)
        public async Task<PaymentResultDto> ProcessPaymentAsync(Guid userId, ProcessPaymentDto dto)
        {
            try
            {
                // Simulate payment processing
                var random = new Random();
                var success = random.Next(1, 10) > 2; // 80% success rate for demo

                var transactionId = success ? $"txn_{Guid.NewGuid():N}" : null;
                var status = success ? PaymentStatus.Success : PaymentStatus.Failed;
                var failureReason = success ? null : "Insufficient funds";

                // Create payment record
                var paymentRecord = await CreatePaymentRecordAsync(userId, new CreatePaymentHistoryDto(
                    dto.PaymentCardId,
                    dto.SubscriptionPlanId,
                    9.99m, // Get from subscription plan
                    "USD",
                    "Premium Monthly",
                    status,
                    transactionId,
                    "Card",
                    failureReason
                ));

                // If payment is successful and user wants to save card
                if (success && dto.SaveCard && dto.CardNumber != null)
                {
                    await AddPaymentCardAsync(userId, new CreatePaymentCardDto(
                        dto.CardNumber,
                        dto.CardholderName ?? "Cardholder",
                        dto.ExpiryMonth ?? "12",
                        dto.ExpiryYear ?? "25",
                        dto.CardType ?? "Visa",
                        false
                    ));
                }

                return new PaymentResultDto(
                    success,
                    success ? "Payment processed successfully" : "Payment failed",
                    transactionId,
                    paymentRecord
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for user {UserId}", userId);
                return new PaymentResultDto(false, "Payment processing error", null, null);
            }
        }

        // Private helper methods
        private static string MaskCardNumber(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
                return cardNumber;

            return $"**** **** **** {cardNumber.Substring(cardNumber.Length - 4)}";
        }

        private static string GetLastFourDigits(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
                return string.Empty;

            return cardNumber.Substring(cardNumber.Length - 4);
        }

        private static PaymentCardDto ToPaymentCardDto(PaymentCard paymentCard)
        {
            return new PaymentCardDto(
                paymentCard.Id,
                paymentCard.UserId,
                paymentCard.CardNumber,
                paymentCard.CardholderName,
                paymentCard.ExpiryMonth,
                paymentCard.ExpiryYear,
                paymentCard.CardType,
                paymentCard.IsDefault,
                paymentCard.IsActive,
                paymentCard.LastFourDigits
            );
        }

        private async Task<PaymentHistoryDto> ToPaymentHistoryDtoAsync(PaymentHistory paymentHistory)
        {
            PaymentCardDto? paymentCardDto = null;
            SubscriptionPlanDto? subscriptionPlanDto = null;

            if (paymentHistory.PaymentCard != null)
            {
                paymentCardDto = ToPaymentCardDto(paymentHistory.PaymentCard);
            }

            if (paymentHistory.SubscriptionPlan != null)
            {
                // Create a simple subscription plan DTO for payment history
                subscriptionPlanDto = new SubscriptionPlanDto(
                    paymentHistory.SubscriptionPlan.Id,
                    paymentHistory.SubscriptionPlan.Name,
                    paymentHistory.SubscriptionPlan.Description,
                    paymentHistory.SubscriptionPlan.Price,
                    paymentHistory.SubscriptionPlan.BillingCycle,
                    paymentHistory.SubscriptionPlan.IsActive,
                    paymentHistory.SubscriptionPlan.SortOrder,
                    paymentHistory.SubscriptionPlan.OriginalPrice,
                    paymentHistory.SubscriptionPlan.IsPopular,
                    new List<SubscriptionFeatureDto>() // Empty for payment history
                );
            }

            return new PaymentHistoryDto(
                paymentHistory.Id,
                paymentHistory.UserId,
                paymentHistory.PaymentCardId,
                paymentHistory.SubscriptionPlanId,
                paymentHistory.Amount,
                paymentHistory.Currency,
                paymentHistory.Description,
                paymentHistory.Status,
                paymentHistory.TransactionId,
                paymentHistory.PaymentMethod,
                paymentHistory.FailureReason,
                paymentHistory.TransactionDate,
                paymentCardDto,
                subscriptionPlanDto
            );
        }
    }
}

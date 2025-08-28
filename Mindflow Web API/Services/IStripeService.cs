using Mindflow_Web_API.DTOs;
using Stripe;

namespace Mindflow_Web_API.Services
{
    public interface IStripeService
    {
        Task<Customer> CreateStripeCustomer(CreateCustomerResource resource, CancellationToken cancellationToken);
        Task<CustomerResource> CreateCustomer(CreateCustomerResource resource, CancellationToken cancellationToken);
        Task<ChargeResource> CreateCharge(CreateChargeResource resource, CancellationToken cancellationToken);
        Task<IEnumerable<ChargeResource>> GetChargeHistory(string customerId, CancellationToken cancellationToken);
        Task<PaymentSheetResource> CreatePaymentSheet(Guid userId, CreatePaymentSheetResource resource, CancellationToken cancellationToken);
        
        // New methods for customer cards
        Task<CustomerCardsResource> GetCustomerCards(string customerId, CancellationToken cancellationToken);
        Task<CustomerCardsResource> GetCustomerCardsByUserId(Guid userId, CancellationToken cancellationToken);
        Task<bool> DeleteCustomerCard(string customerId, string paymentMethodId, CancellationToken cancellationToken);
        Task<PaymentMethodResource> SetDefaultCard(string customerId, string paymentMethodId, CancellationToken cancellationToken);
    }
}

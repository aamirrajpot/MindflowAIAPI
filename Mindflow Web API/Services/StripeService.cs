using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Microsoft.Extensions.Options;
using Mindflow_Web_API.Persistence;
using Stripe;

namespace Mindflow_Web_API.Services
{
    public class StripeService : IStripeService
    {
        private readonly CustomerService _customerService;
        private readonly ChargeService _chargeService;
        private readonly PaymentIntentService _paymentIntentService;
        private readonly PaymentMethodService _paymentMethodService;
        private readonly StripeOptions _stripeOptions;
        private readonly EphemeralKeyService _ephemeralKeyService;
        private readonly MindflowDbContext _dbContext;

        public StripeService(
            CustomerService customerService,
            ChargeService chargeService,
            PaymentIntentService paymentIntentService,
            PaymentMethodService paymentMethodService,
            IOptions<StripeOptions> stripeOptions,
            EphemeralKeyService ephemeralKeyService,
            MindflowDbContext dbContext)
        {
            _customerService = customerService;
            _chargeService = chargeService;
            _paymentIntentService = paymentIntentService;
            _paymentMethodService = paymentMethodService;
            _stripeOptions = stripeOptions.Value;
            _ephemeralKeyService = ephemeralKeyService;
            _dbContext = dbContext;

            // Set Stripe API key
            StripeConfiguration.ApiKey = _stripeOptions.SecretKey;
        }

        public async Task<Customer> CreateStripeCustomer(CreateCustomerResource resource, CancellationToken cancellationToken)
        {
            var customerOptions = new CustomerCreateOptions
            {
                Email = resource.Email,
                Name = resource.Name
            };
            
            return await _customerService.CreateAsync(customerOptions, cancellationToken: cancellationToken);
        }

        public async Task<CustomerResource> CreateCustomer(CreateCustomerResource resource, CancellationToken cancellationToken)
        {
            var customerOptions = new CustomerCreateOptions
            {
                Email = resource.Email,
                Name = resource.Name
            };
            var customer = await _customerService.CreateAsync(customerOptions, cancellationToken: cancellationToken);

            return new CustomerResource(customer.Id, customer.Email, customer.Name);
        }

        public async Task<ChargeResource> CreateCharge(CreateChargeResource resource, CancellationToken cancellationToken)
        {
            var chargeOptions = new ChargeCreateOptions
            {
                Currency = resource.Currency,
                Amount = resource.Amount,
                ReceiptEmail = resource.ReceiptEmail,
                Customer = resource.CustomerId,
                Description = resource.Description
            };

            var charge = await _chargeService.CreateAsync(chargeOptions, null, cancellationToken);

            return new ChargeResource(charge.Id, charge.Currency, charge.Amount, charge.CustomerId, charge.ReceiptEmail,
                charge.Description, charge.Created);
        }

        public async Task<IEnumerable<ChargeResource>> GetChargeHistory(string customerId, CancellationToken cancellationToken)
        {
            try
            {
                var chargeListOptions = new ChargeListOptions
                {
                    Customer = customerId,
                    Limit = 10 // Limit to 10 charges; you can adjust as needed
                };

                var charges = await _chargeService.ListAsync(chargeListOptions, null, cancellationToken);

                return charges.Data.Select(charge => new ChargeResource(
                    charge.Id,
                    charge.Currency,
                    charge.Amount,
                    charge.CustomerId,
                    charge.ReceiptEmail,
                    charge.Description,
                    charge.Created
                ));
            }
            catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Return empty array if no charges found
                return Enumerable.Empty<ChargeResource>();
            }
            // Let other exceptions propagate up
        }

        public async Task<PaymentSheetResource> CreatePaymentSheet(Guid userId, CreatePaymentSheetResource resource, CancellationToken cancellationToken)
        {
            // Get user to check if they already have a Stripe customer ID
            var user = await _dbContext.Users.FindAsync(new object?[] { userId }, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"User with ID {userId} not found.");

            // Create or use existing customer
            Customer customer;
            if (!string.IsNullOrEmpty(user.StripeCustomerId))
            {
                // User already has a Stripe customer ID, use it
                customer = await _customerService.GetAsync(user.StripeCustomerId, cancellationToken: cancellationToken);
            }
            else if (!string.IsNullOrEmpty(resource.CustomerId))
            {
                // Use provided customer ID
                customer = await _customerService.GetAsync(resource.CustomerId, cancellationToken: cancellationToken);
            }
            else
            {
                // Create new customer
                var customerOptions = new CustomerCreateOptions
                {
                    Email = resource.Email,
                    Name = resource.Name
                };
                customer = await _customerService.CreateAsync(customerOptions, cancellationToken: cancellationToken);
                
                // Save the customer ID to the user record
                user.StripeCustomerId = customer.Id;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // If a plan is specified, use its price for the amount
            decimal amount = resource.Amount;
            if (resource.PlanId.HasValue)
            {
                var plan = await _dbContext.SubscriptionPlans.FindAsync(new object?[] { resource.PlanId.Value }, cancellationToken: cancellationToken)
                    ?? throw new InvalidOperationException($"Subscription Plan with ID {resource.PlanId.Value} not found.");
                amount = plan.Price;
            }

            // Convert amount to smallest currency unit (cents for USD)
            var amountInSmallestUnit = ConvertToSmallestCurrencyUnit(amount, resource.Currency);

            // Create a PaymentIntent
            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = amountInSmallestUnit,
                Currency = resource.Currency.ToLower(),
                Customer = customer.Id,
                PaymentMethodTypes = new List<string>
                {
                    "card",
                },
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId.ToString() },
                    { "amount", amountInSmallestUnit.ToString() },
                    { "currency", resource.Currency.ToLower() },
                    { "planId", resource.PlanId?.ToString() ?? string.Empty }
                }
            };

            var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions, cancellationToken: cancellationToken);

            // Return the payment sheet parameters
            var ephemeralKeySecret = await CreateEphemeralKey(customer.Id, cancellationToken);
            return new PaymentSheetResource(
                paymentIntent.ClientSecret,
                customer.Id,
                ephemeralKeySecret,
                _stripeOptions.PublishableKey
            );
        }

        public async Task<CustomerCardsResource> GetCustomerCards(string customerId, CancellationToken cancellationToken)
        {
            try
            {
                var paymentMethodListOptions = new PaymentMethodListOptions
                {
                    Customer = customerId,
                    Type = "card"
                };

                var paymentMethods = await _paymentMethodService.ListAsync(paymentMethodListOptions, cancellationToken: cancellationToken);

                var cards = paymentMethods.Data.Select(pm => new PaymentMethodResource(
                    pm.Id,
                    pm.Type,
                    pm.Card?.Brand,
                    pm.Card?.Last4,
                    (int?)pm.Card?.ExpMonth,
                    (int?)pm.Card?.ExpYear,
                    pm.Card?.Country,
                    pm.Card?.Funding,
                    false, // Stripe doesn't have a concept of default payment method at the customer level
                    pm.Created
                ));

                return new CustomerCardsResource(customerId, cards);
            }
            catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Return empty list if customer not found
                return new CustomerCardsResource(customerId, Enumerable.Empty<PaymentMethodResource>());
            }
        }

        public async Task<CustomerCardsResource> GetCustomerCardsByUserId(Guid userId, CancellationToken cancellationToken)
        {
            // Get user to find their Stripe customer ID
            var user = await _dbContext.Users.FindAsync(new object?[] { userId }, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"User with ID {userId} not found.");

            if (string.IsNullOrEmpty(user.StripeCustomerId))
            {
                // User doesn't have a Stripe customer ID yet
                return new CustomerCardsResource(string.Empty, Enumerable.Empty<PaymentMethodResource>());
            }

            return await GetCustomerCards(user.StripeCustomerId, cancellationToken);
        }

        public async Task<bool> DeleteCustomerCard(string customerId, string paymentMethodId, CancellationToken cancellationToken)
        {
            try
            {
                // First, detach the payment method from the customer
                var detachOptions = new PaymentMethodDetachOptions();
                await _paymentMethodService.DetachAsync(paymentMethodId, detachOptions, cancellationToken: cancellationToken);
                
                return true;
            }
            catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Payment method not found
                return false;
            }
        }

        public async Task<PaymentMethodResource> SetDefaultCard(string customerId, string paymentMethodId, CancellationToken cancellationToken)
        {
            try
            {
                // Update the customer's default payment method
                var customerUpdateOptions = new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = paymentMethodId
                    }
                };

                await _customerService.UpdateAsync(customerId, customerUpdateOptions, cancellationToken: cancellationToken);

                // Get the updated payment method
                var paymentMethod = await _paymentMethodService.GetAsync(paymentMethodId, cancellationToken: cancellationToken);

                return new PaymentMethodResource(
                    paymentMethod.Id,
                    paymentMethod.Type,
                    paymentMethod.Card?.Brand,
                    paymentMethod.Card?.Last4,
                    (int?)paymentMethod.Card?.ExpMonth,
                    (int?)paymentMethod.Card?.ExpYear,
                    paymentMethod.Card?.Country,
                    paymentMethod.Card?.Funding,
                    true, // This is now the default
                    paymentMethod.Created
                );
            }
            catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Customer or payment method not found.");
            }
        }

        private static long ConvertToSmallestCurrencyUnit(decimal amount, string currency)
        {
            // Most currencies are converted by multiplying by 100 (for cents)
            // but some currencies like JPY don't have decimals
            currency = currency.ToUpper();
            
            return currency switch
            {
                // Zero-decimal currencies (no conversion needed)
                "JPY" => (long)amount, // Japanese Yen
                "VND" => (long)amount, // Vietnamese Dong
                "KRW" => (long)amount, // Korean Won
                
                // Two-decimal currencies (multiply by 100)
                _ => (long)(amount * 100) // USD, EUR, GBP, etc.
            };
        }

        private async Task<string> CreateEphemeralKey(string customerId, CancellationToken cancellationToken)
        {
            var ephemeralKeyOptions = new EphemeralKeyCreateOptions
            {
                Customer = customerId,
            };

            var ephemeralKey = await _ephemeralKeyService.CreateAsync(ephemeralKeyOptions, cancellationToken: cancellationToken);
            return ephemeralKey.Secret;
        }
    }
}

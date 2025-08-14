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
        private readonly StripeOptions _stripeOptions;
        private readonly EphemeralKeyService _ephemeralKeyService;
        private readonly MindflowDbContext _dbContext;

        public StripeService(
            CustomerService customerService,
            ChargeService chargeService,
            PaymentIntentService paymentIntentService,
            IOptions<StripeOptions> stripeOptions,
            EphemeralKeyService ephemeralKeyService,
            MindflowDbContext dbContext)
        {
            _customerService = customerService;
            _chargeService = chargeService;
            _paymentIntentService = paymentIntentService;
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

        public async Task<PaymentSheetResource> CreatePaymentSheet(CreatePaymentSheetResource resource, CancellationToken cancellationToken)
        {
            // Create or use existing customer
            Customer customer;
            if (string.IsNullOrEmpty(resource.CustomerId))
            {
                var customerOptions = new CustomerCreateOptions
                {
                    Email = resource.Email,
                    Name = resource.Name
                };
                customer = await _customerService.CreateAsync(customerOptions, cancellationToken: cancellationToken);
            }
            else
            {
                customer = await _customerService.GetAsync(resource.CustomerId, cancellationToken: cancellationToken);
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
                    { "userId", resource.UserId },
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

using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Microsoft.AspNetCore.Authorization;
using Stripe;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace Mindflow_Web_API.EndPoints
{
    public static class StripeEndpoints
    {
        public static void MapStripeEndpoints(this WebApplication app)
        {
            var stripeApi = app.MapGroup("/api/stripe").WithTags("Stripe").RequireAuthorization();

            // Create customer (no card token)
            stripeApi.MapPost("/customers", async (CreateCustomerResource resource, IStripeService stripeService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var customer = await stripeService.CreateCustomer(resource, cancellationToken);
                    return Results.Ok(customer);
                }
                catch (StripeException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Create a basic Stripe customer (raw)
            stripeApi.MapPost("/customers/simple", async (CreateCustomerResource resource, IStripeService stripeService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var customer = await stripeService.CreateStripeCustomer(resource, cancellationToken);
                    return Results.Ok(new { id = customer.Id, email = customer.Email, name = customer.Name });
                }
                catch (StripeException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Create charge against existing customer
            stripeApi.MapPost("/charges", async (CreateChargeResource resource, IStripeService stripeService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var charge = await stripeService.CreateCharge(resource, cancellationToken);
                    return Results.Ok(charge);
                }
                catch (StripeException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Get charge history for a customer
            stripeApi.MapGet("/charges/{customerId}", async (string customerId, IStripeService stripeService, CancellationToken cancellationToken) =>
            {
                try
                {
                    var charges = await stripeService.GetChargeHistory(customerId, cancellationToken);
                    return Results.Ok(charges);
                }
                catch (StripeException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Create PaymentSheet (optionally with PlanId; userId extracted from token)
            stripeApi.MapPost("/payment-sheet", async (CreatePaymentSheetResource resource, IStripeService stripeService, HttpContext context, CancellationToken cancellationToken) =>
            {
                try
                {
                    // Extract userId from JWT token
                    var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                        return Results.Unauthorized();

                    var paymentSheet = await stripeService.CreatePaymentSheet(userId, resource, cancellationToken);
                    return Results.Ok(paymentSheet);
                }
                catch (StripeException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Webhook endpoint (no auth) for Stripe events
            app.MapPost("/api/stripe/webhook", async (HttpRequest request, IConfiguration configuration, MindflowDbContext dbContext, IServiceProvider serviceProvider) =>
            {
                var json = await new StreamReader(request.Body).ReadToEndAsync();
                var signature = request.Headers["Stripe-Signature"].FirstOrDefault();
                var webhookSecret = configuration["Stripe:WebhookSecret"];

                if (string.IsNullOrWhiteSpace(webhookSecret))
                {
                    return Results.Problem("Stripe webhook secret is not configured.", statusCode: 500);
                }

                Event stripeEvent;
                try
                {
                    stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = $"Webhook signature verification failed: {ex.Message}" });
                }

                if (stripeEvent.Type == "payment_intent.succeeded")
                {
                    var paymentIntent = (PaymentIntent)stripeEvent.Data.Object;

                    // Prevent duplicates
                    var exists = await dbContext.PaymentHistory.AnyAsync(p => p.TransactionId == paymentIntent.Id);
                    if (!exists)
                    {
                        var metadata = paymentIntent.Metadata ?? new Dictionary<string, string>();
                        Guid.TryParse(metadata.GetValueOrDefault("userId"), out var userId);
                        Guid? planId = null;
                        if (Guid.TryParse(metadata.GetValueOrDefault("planId"), out var parsedPlan))
                        {
                            planId = parsedPlan;
                        }

                        var currency = (metadata.GetValueOrDefault("currency") ?? paymentIntent.Currency ?? "usd").ToUpper();
                        long amountMinor = paymentIntent.AmountReceived > 0 ? paymentIntent.AmountReceived : paymentIntent.Amount;
                        decimal amountMajor = currency is "JPY" or "VND" or "KRW" ? amountMinor : amountMinor / 100m;

                        var record = new PaymentHistory
                        {
                            UserId = userId,
                            PaymentCardId = null,
                            SubscriptionPlanId = planId,
                            Amount = amountMajor,
                            Currency = currency,
                            Description = paymentIntent.Description ?? "PaymentIntent Succeeded",
                            Status = PaymentStatus.Success,
                            TransactionId = paymentIntent.Id,
                            PaymentMethod = paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                            FailureReason = null,
                            TransactionDate = DateTime.UtcNow
                        };

                        await dbContext.PaymentHistory.AddAsync(record);
                        await dbContext.SaveChangesAsync();

                        // Create user subscription if planId is provided
                        if (planId.HasValue && userId != Guid.Empty)
                        {
                            try
                            {
                                // Get subscription service from DI container
                                using var scope = serviceProvider.CreateScope();
                                var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                                
                                // Create user subscription
                                var createSubscriptionDto = new CreateUserSubscriptionDto(planId.Value);
                                await subscriptionService.CreateUserSubscriptionAsync(userId, createSubscriptionDto);
                                
                                // Log successful subscription creation
                                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                                logger.LogInformation("User subscription created successfully for UserId: {UserId}, PlanId: {PlanId}, PaymentIntent: {PaymentIntentId}", 
                                    userId, planId.Value, paymentIntent.Id);
                            }
                            catch (Exception ex)
                            {
                                // Log error but don't fail the webhook
                                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                                logger.LogError(ex, "Failed to create user subscription for UserId: {UserId}, PlanId: {PlanId}, PaymentIntent: {PaymentIntentId}", 
                                    userId, planId.Value, paymentIntent.Id);
                            }
                        }
                    }
                }

                // Return 200 OK for all events to acknowledge receipt
                return Results.Ok(new { received = true });
                         }).AllowAnonymous().WithTags("Stripe");

            // Test webhook endpoint (for development only)
            app.MapPost("/api/stripe/test-webhook", async (TestWebhookDto dto, IConfiguration configuration, MindflowDbContext dbContext, IServiceProvider serviceProvider) =>
            {
                // Only allow in development
                var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
                if (!environment.IsDevelopment())
                {
                    return Results.Forbid();
                }

                // Create a mock PaymentIntent
                var mockPaymentIntent = new PaymentIntent
                {
                    Id = dto.PaymentIntentId ?? $"pi_test_{Guid.NewGuid():N}",
                    Amount = dto.Amount ?? 1000, // $10.00 in cents
                    Currency = dto.Currency ?? "usd",
                    Status = "succeeded",
                    PaymentMethodTypes = new List<string> { "card" },
                    Description = dto.Description ?? "Test Payment",
                    Metadata = new Dictionary<string, string>
                    {
                        { "userId", dto.UserId?.ToString() ?? Guid.Empty.ToString() },
                        { "planId", dto.PlanId?.ToString() ?? string.Empty },
                        { "currency", dto.Currency ?? "usd" },
                        { "amount", (dto.Amount ?? 1000).ToString() }
                    }
                };

                // Simulate the webhook processing
                var exists = await dbContext.PaymentHistory.AnyAsync(p => p.TransactionId == mockPaymentIntent.Id);
                var metadata = mockPaymentIntent.Metadata ?? new Dictionary<string, string>();
                Guid.TryParse(metadata.GetValueOrDefault("userId"), out var userId);
                Guid? planId = null;
                if (Guid.TryParse(metadata.GetValueOrDefault("planId"), out var parsedPlan))
                {
                    planId = parsedPlan;
                }
                
                if (!exists)
                {

                    var currency = (metadata.GetValueOrDefault("currency") ?? mockPaymentIntent.Currency ?? "usd").ToUpper();
                    long amountMinor = mockPaymentIntent.Amount;
                    decimal amountMajor = currency is "JPY" or "VND" or "KRW" ? amountMinor : amountMinor / 100m;

                    var record = new PaymentHistory
                    {
                        UserId = userId,
                        PaymentCardId = null,
                        SubscriptionPlanId = planId,
                        Amount = amountMajor,
                        Currency = currency,
                        Description = mockPaymentIntent.Description ?? "Test PaymentIntent Succeeded",
                        Status = PaymentStatus.Success,
                        TransactionId = mockPaymentIntent.Id,
                        PaymentMethod = mockPaymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                        FailureReason = null,
                        TransactionDate = DateTime.UtcNow
                    };

                    await dbContext.PaymentHistory.AddAsync(record);
                    await dbContext.SaveChangesAsync();

                    // Create user subscription if planId is provided
                    if (planId.HasValue && userId != Guid.Empty)
                    {
                        try
                        {
                            using var scope = serviceProvider.CreateScope();
                            var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                            
                            var createSubscriptionDto = new CreateUserSubscriptionDto(planId.Value);
                            await subscriptionService.CreateUserSubscriptionAsync(userId, createSubscriptionDto);
                            
                            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                            logger.LogInformation("Test webhook: User subscription created successfully for UserId: {UserId}, PlanId: {PlanId}, PaymentIntent: {PaymentIntentId}", 
                                userId, planId.Value, mockPaymentIntent.Id);
                        }
                        catch (Exception ex)
                        {
                            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                            logger.LogError(ex, "Test webhook: Failed to create user subscription for UserId: {UserId}, PlanId: {PlanId}, PaymentIntent: {PaymentIntentId}", 
                                userId, planId.Value, mockPaymentIntent.Id);
                        }
                    }
                }

                return Results.Ok(new { 
                    message = "Test webhook processed successfully",
                    paymentIntentId = mockPaymentIntent.Id,
                    userId = dto.UserId,
                    planId = dto.PlanId,
                    subscriptionCreated = planId.HasValue && userId != Guid.Empty
                });
            }).WithTags("Stripe");
        }
    }
}



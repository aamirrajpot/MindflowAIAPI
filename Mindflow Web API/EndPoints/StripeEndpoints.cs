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
            app.MapPost("/api/stripe/webhook", async (HttpRequest request, IConfiguration configuration, MindflowDbContext dbContext) =>
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
                            TransactionDate = (paymentIntent.Created.Kind == DateTimeKind.Utc ? paymentIntent.Created : paymentIntent.Created.ToUniversalTime())
                        };

                        await dbContext.PaymentHistory.AddAsync(record);
                        await dbContext.SaveChangesAsync();
                    }
                }

                // Return 200 OK for all events to acknowledge receipt
                return Results.Ok(new { received = true });
            }).AllowAnonymous().WithTags("Stripe");
        }
    }
}



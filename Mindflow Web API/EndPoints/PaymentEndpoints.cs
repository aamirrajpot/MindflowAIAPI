using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;

namespace Mindflow_Web_API.EndPoints
{
    public static class PaymentEndpoints
    {
        public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
        {
            var paymentApi = app.MapGroup("/api/payments").WithTags("Payments");

            // Wallet Overview
            paymentApi.MapGet("/wallet", async (IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var wallet = await paymentService.GetWalletOverviewAsync(userId);
                return Results.Ok(wallet);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get wallet overview";
                op.Description = "Gets the user's payment cards and payment history.";
                return op;
            });

            // Payment Cards
            paymentApi.MapGet("/cards", async (IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var cards = await paymentService.GetUserPaymentCardsAsync(userId);
                return Results.Ok(cards);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get payment cards";
                op.Description = "Gets all payment cards for the authenticated user.";
                return op;
            });

            paymentApi.MapPost("/cards", async (CreatePaymentCardDto dto, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var card = await paymentService.AddPaymentCardAsync(userId, dto);
                return Results.Ok(card);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Add payment card";
                op.Description = "Adds a new payment card for the authenticated user.";
                return op;
            });

            paymentApi.MapGet("/cards/{cardId:guid}", async (Guid cardId, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var card = await paymentService.GetPaymentCardByIdAsync(userId, cardId);
                if (card == null)
                    throw ApiExceptions.NotFound("Payment card not found");
                return Results.Ok(card);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get payment card by ID";
                op.Description = "Gets a specific payment card by ID for the authenticated user.";
                return op;
            });

            paymentApi.MapPut("/cards/{cardId:guid}", async (Guid cardId, UpdatePaymentCardDto dto, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var card = await paymentService.UpdatePaymentCardAsync(userId, cardId, dto);
                if (card == null)
                    throw ApiExceptions.NotFound("Payment card not found");
                return Results.Ok(card);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Update payment card";
                op.Description = "Updates a payment card for the authenticated user.";
                return op;
            });

            paymentApi.MapDelete("/cards/{cardId:guid}", async (Guid cardId, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var deleted = await paymentService.DeletePaymentCardAsync(userId, cardId);
                if (!deleted)
                    throw ApiExceptions.NotFound("Payment card not found");
                return Results.Ok(new { message = "Payment card deleted successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Delete payment card";
                op.Description = "Deletes a payment card for the authenticated user.";
                return op;
            });

            paymentApi.MapPatch("/cards/{cardId:guid}/default", async (Guid cardId, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var success = await paymentService.SetDefaultCardAsync(userId, cardId);
                if (!success)
                    throw ApiExceptions.NotFound("Payment card not found");
                return Results.Ok(new { message = "Default card set successfully" });
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Set default payment card";
                op.Description = "Sets a payment card as the default for the authenticated user.";
                return op;
            });

            // Payment History
            paymentApi.MapGet("/history", async (IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var history = await paymentService.GetUserPaymentHistoryAsync(userId);
                return Results.Ok(history);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get payment history";
                op.Description = "Gets the payment history for the authenticated user.";
                return op;
            });

            paymentApi.MapGet("/history/{paymentId:guid}", async (Guid paymentId, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var payment = await paymentService.GetPaymentHistoryByIdAsync(userId, paymentId);
                if (payment == null)
                    throw ApiExceptions.NotFound("Payment record not found");
                return Results.Ok(payment);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Get payment by ID";
                op.Description = "Gets a specific payment record by ID for the authenticated user.";
                return op;
            });

            // Payment Processing
            paymentApi.MapPost("/process", async (ProcessPaymentDto dto, IPaymentService paymentService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");
                var result = await paymentService.ProcessPaymentAsync(userId, dto);
                return Results.Ok(result);
            })
            .RequireAuthorization()
            .WithOpenApi(op => {
                op.Summary = "Process payment";
                op.Description = "Processes a payment for subscription or other services.";
                return op;
            });
        }
    }
}

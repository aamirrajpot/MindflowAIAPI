using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Mindflow_Web_API.EndPoints
{
    public static class JournalEndpoints
    {
        public static void MapJournalEndpoints(this IEndpointRouteBuilder app)
        {
            var journal = app.MapGroup("/journal").WithTags("Journal");

            // Get journal statistics
            journal.MapGet("/stats", async (IJournalService journalService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var stats = await journalService.GetStatsAsync(userId);
                return Results.Ok(stats);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get journal statistics";
                op.Description = "Returns journal statistics including total entries, current streak, and word count.";
                return op;
            });

            // Get recent entries
            journal.MapGet("/recent", async (IJournalService journalService, HttpContext context, [FromQuery] int count = 5) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var entries = await journalService.GetRecentEntriesAsync(userId, count);
                return Results.Ok(entries);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get recent journal entries";
                op.Description = "Returns the most recent journal entries for the authenticated user.";
                return op;
            });

            // Search and filter entries
            journal.MapGet("/entries", async (IJournalService journalService, HttpContext context, 
                [FromQuery] string? query = null, 
                [FromQuery] string? filter = null, 
                [FromQuery] int page = 1, 
                [FromQuery] int pageSize = 10) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var searchDto = new JournalSearchDto(query, filter, page, pageSize);
                var result = await journalService.GetEntriesAsync(userId, searchDto);
                return Results.Ok(result);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Search and filter journal entries";
                op.Description = "Returns journal entries with optional search query and filtering (all, recent, favorites).";
                return op;
            });

            // Create new journal entry
            journal.MapPost("/entries", async (CreateJournalEntryDto dto, IJournalService journalService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var entry = await journalService.CreateEntryAsync(userId, dto);
                return Results.Created($"/journal/entries/{entry.Id}", entry);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new journal entry";
                op.Description = "Creates a new journal entry for the authenticated user.";
                return op;
            });

            // Get specific journal entry
            journal.MapGet("/entries/{entryId:guid}", async (Guid entryId, IJournalService journalService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var entry = await journalService.GetEntryByIdAsync(userId, entryId);
                if (entry == null)
                    throw ApiExceptions.NotFound("Journal entry not found");
                
                return Results.Ok(entry);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get a specific journal entry";
                op.Description = "Retrieves a specific journal entry by ID for the authenticated user.";
                return op;
            });

            // Update journal entry
            journal.MapPut("/entries/{entryId:guid}", async (Guid entryId, UpdateJournalEntryDto dto, IJournalService journalService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var entry = await journalService.UpdateEntryAsync(userId, entryId, dto);
                if (entry == null)
                    throw ApiExceptions.NotFound("Journal entry not found");
                
                return Results.Ok(entry);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Update a journal entry";
                op.Description = "Updates a specific journal entry for the authenticated user.";
                return op;
            });

            // Delete journal entry
            journal.MapDelete("/entries/{entryId:guid}", async (Guid entryId, IJournalService journalService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var deleted = await journalService.DeleteEntryAsync(userId, entryId);
                if (!deleted)
                    throw ApiExceptions.NotFound("Journal entry not found");
                
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Delete a journal entry";
                op.Description = "Soft deletes a specific journal entry for the authenticated user.";
                return op;
            });

            // Generate AI insight for an entry
            journal.MapPost("/entries/{entryId:guid}/insight", async (Guid entryId, IJournalService journalService, HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var insight = await journalService.GenerateAiInsightAsync(userId, entryId);
                if (string.IsNullOrEmpty(insight))
                    throw ApiExceptions.NotFound("Journal entry not found or insight generation failed");
                
                return Results.Ok(new { insight });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Generate AI insight for journal entry";
                op.Description = "Generates an AI-powered insight for a specific journal entry.";
                return op;
            });

            // Get AI insights
            journal.MapGet("/insights", async (IJournalService journalService, HttpContext context, [FromQuery] int count = 10) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");
                
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                var insights = await journalService.GetAiInsightsAsync(userId, count);
                return Results.Ok(insights);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get AI insights";
                op.Description = "Returns AI-generated insights for the user's journal entries.";
                return op;
            });
        }
    }
}

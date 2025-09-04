using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Mindflow_Web_API.Persistence;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.EndPoints
{
	public static class BrainDumpEndpoints
	{
		public static IEndpointRouteBuilder MapBrainDumpEndpoints(this IEndpointRouteBuilder app)
		{
			var api = app.MapGroup("/brain-dump").WithTags("Brain Dump").RequireAuthorization();

			api.MapPost("/suggestions", async (
				[FromBody] BrainDumpRequest request,
				IBrainDumpService service,
				HttpContext ctx,
				MindflowDbContext db,
				[FromQuery] int maxTokens = 1200,
				[FromQuery] double temperature = 0.7) =>
			{
				if (!ctx.User.Identity?.IsAuthenticated ?? true)
					return Results.Unauthorized();

				// Resolve user id
				var userIdClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
				if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
					return Results.Unauthorized();

				// Generate suggestions (service persists entry)
				var suggestions = await service.GetTaskSuggestionsAsync(request, maxTokens, temperature);

				// Attach user to most recent brain dump entry created in this request scope
				var latest = await db.BrainDumpEntries.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync();
				if (latest != null && latest.UserId == Guid.Empty)
				{
					latest.UserId = userId;
					await db.SaveChangesAsync();
				}

				return Results.Ok(new { suggestions });
			})
			.WithOpenApi(op =>
			{
				op.Summary = "Get AI task suggestions from a brain dump";
				op.Description = "Accepts free-form text and returns 3-5 structured task suggestions";
				return op;
			});

			return app;
		}
	}
}



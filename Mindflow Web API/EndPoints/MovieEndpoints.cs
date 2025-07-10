using Serilog;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mindflow_Web_API.EndPoints
{
    public static class MovieEndpoints
    {
        public static void MapMovieEndpoints(this IEndpointRouteBuilder routes)
        {
            var movieApi = routes.MapGroup("/api/movies").WithTags("Movies");

            movieApi.MapPost("/", async (HttpContext context) =>
            {
                var service = context.RequestServices.GetRequiredService<IMovieService>();
                var command = await context.Request.ReadFromJsonAsync<CreateMovieDto>();
                if (command is null)
                    return Results.BadRequest(new ApiResponseDto<MovieDto>(false, "Invalid request body"));
                var response = await service.CreateMovieAsync(command);
                return Results.Created($"/api/movies/{response.Data?.Id}", response);
            });

            movieApi.MapGet("/", async ([FromServices] IMovieService service) =>
            {
                var response = await service.GetAllMoviesAsync();
                return Results.Ok(response);
            });

            movieApi.MapGet("/{id}", async (Guid id, [FromServices] IMovieService service) =>
            {
                var response = await service.GetMovieByIdAsync(id);
                if (!response.Success)
                    return Results.NotFound(response);
                return Results.Ok(response);
            });

            movieApi.MapPut("/{id}", async (HttpContext context, Guid id) =>
            {
                var service = context.RequestServices.GetRequiredService<IMovieService>();
                var command = await context.Request.ReadFromJsonAsync<UpdateMovieDto>();
                if (command is null)
                    return Results.BadRequest(new ApiResponseDto<bool>(false, "Invalid request body"));
                var response = await service.UpdateMovieAsync(id, command);
                if (!response.Success)
                    return Results.BadRequest(response);
                return Results.Ok(response);
            });

            movieApi.MapDelete("/{id}", async (Guid id, [FromServices] IMovieService service) =>
            {
                var response = await service.DeleteMovieAsync(id);
                if (!response.Success)
                    return Results.BadRequest(response);
                return Results.Ok(response);
            });
        }
    }
}

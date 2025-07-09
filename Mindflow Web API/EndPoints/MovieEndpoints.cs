using Serilog;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Services;

namespace Mindflow_Web_API.EndPoints
{
    public static class MovieEndpoints
    {
        public static void MapMovieEndpoints(this IEndpointRouteBuilder routes)
        {
            var movieApi = routes.MapGroup("/api/movies").WithTags("Movies");

            movieApi.MapPost("/", async (IMovieService service, CreateMovieDto command) =>
            {
                Log.Information("Creating new movie: {@Command}", command);
                var movie = await service.CreateMovieAsync(command);
                Log.Information("Movie created successfully with ID: {MovieId}", movie.Id);
                return TypedResults.Created($"/api/movies/{movie.Id}", movie);
            });

            movieApi.MapGet("/", async (IMovieService service) =>
            {
                Log.Information("Retrieving all movies");
                var movies = await service.GetAllMoviesAsync();
                Log.Information("Retrieved {Count} movies", movies.Count());
                return TypedResults.Ok(movies);
            });

            movieApi.MapGet("/{id}", async (IMovieService service, Guid id) =>
            {
                Log.Information("Retrieving movie with ID: {MovieId}", id);
                var movie = await service.GetMovieByIdAsync(id);

                if (movie is null)
                {
                    Log.Warning("Movie not found with ID: {MovieId}", id);
                    return (IResult)TypedResults.NotFound(new { Message = $"Movie with ID {id} not found." });
                }

                Log.Information("Retrieved movie: {@Movie}", movie);
                return TypedResults.Ok(movie);
            });

            movieApi.MapPut("/{id}", async (IMovieService service, Guid id, UpdateMovieDto command) =>
            {
                Log.Information("Updating movie with ID: {MovieId}, Command: {@Command}", id, command);
                await service.UpdateMovieAsync(id, command);
                Log.Information("Movie updated successfully");
                return TypedResults.NoContent();
            });

            movieApi.MapDelete("/{id}", async (IMovieService service, Guid id) =>
            {
                Log.Information("Deleting movie with ID: {MovieId}", id);
                await service.DeleteMovieAsync(id);
                Log.Information("Movie deleted successfully");
                return TypedResults.NoContent();
            });
        }
    }
}

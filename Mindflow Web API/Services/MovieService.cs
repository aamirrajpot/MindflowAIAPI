using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    public class MovieService : IMovieService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<MovieService> _logger;

        public MovieService(MindflowDbContext dbContext, ILogger<MovieService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ApiResponseDto<MovieDto>> CreateMovieAsync(CreateMovieDto command)
        {
            try
            {
                var movie = Movie.Create(command.Title, command.Genre, command.ReleaseDate, command.Rating);
                await _dbContext.Movies.AddAsync(movie);
                await _dbContext.SaveChangesAsync();
                var dto = new MovieDto(movie.Id, movie.Title, movie.Genre, movie.ReleaseDate, movie.Rating);
                return new ApiResponseDto<MovieDto>(true, "Movie created successfully", dto);
            }
            catch (Exception ex)
            {
                return new ApiResponseDto<MovieDto>(false, "Failed to create movie", null, ex.Message);
            }
        }

        public async Task<ApiResponseDto<bool>> DeleteMovieAsync(Guid id)
        {
            try
            {
                var movieToDelete = await _dbContext.Movies.FindAsync(id);
                if (movieToDelete != null)
                {
                    _dbContext.Movies.Remove(movieToDelete);
                    await _dbContext.SaveChangesAsync();
                    return new ApiResponseDto<bool>(true, "Movie deleted successfully", true);
                }
                return new ApiResponseDto<bool>(false, "Movie not found.", false);
            }
            catch (Exception ex)
            {
                return new ApiResponseDto<bool>(false, "Failed to delete movie", false, ex.Message);
            }
        }

        public async Task<ApiResponseDto<IEnumerable<MovieDto>>> GetAllMoviesAsync()
        {
            try
            {
                var movies = await _dbContext.Movies
                    .AsNoTracking()
                    .Select(movie => new MovieDto(
                        movie.Id,
                        movie.Title,
                        movie.Genre,
                        movie.ReleaseDate,
                        movie.Rating
                    ))
                    .ToListAsync();
                return new ApiResponseDto<IEnumerable<MovieDto>>(true, "Movies retrieved successfully", movies);
            }
            catch (Exception ex)
            {
                return new ApiResponseDto<IEnumerable<MovieDto>>(false, "Failed to retrieve movies", null, ex.Message);
            }
        }

        public async Task<ApiResponseDto<MovieDto?>> GetMovieByIdAsync(Guid id)
        {
            try
            {
                var movie = await _dbContext.Movies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (movie == null)
                    return new ApiResponseDto<MovieDto?>(false, $"Movie with ID {id} not found.", null);
                var dto = new MovieDto(movie.Id, movie.Title, movie.Genre, movie.ReleaseDate, movie.Rating);
                return new ApiResponseDto<MovieDto?>(true, "Movie retrieved successfully", dto);
            }
            catch (Exception ex)
            {
                return new ApiResponseDto<MovieDto?>(false, "Failed to retrieve movie", null, ex.Message);
            }
        }

        public async Task<ApiResponseDto<bool>> UpdateMovieAsync(Guid id, UpdateMovieDto command)
        {
            try
            {
                var movieToUpdate = await _dbContext.Movies.FindAsync(id);
                if (movieToUpdate is null)
                    return new ApiResponseDto<bool>(false, "Invalid Movie Id.", false);
                movieToUpdate.Update(command.Title, command.Genre, command.ReleaseDate, command.Rating);
                await _dbContext.SaveChangesAsync();
                return new ApiResponseDto<bool>(true, "Movie updated successfully", true);
            }
            catch (Exception ex)
            {
                return new ApiResponseDto<bool>(false, "Failed to update movie", false, ex.Message);
            }
        }
    }
}

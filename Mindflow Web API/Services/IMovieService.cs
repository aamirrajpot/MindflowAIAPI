using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IMovieService
    {
        Task<ApiResponseDto<MovieDto>> CreateMovieAsync(CreateMovieDto command);
        Task<ApiResponseDto<IEnumerable<MovieDto>>> GetAllMoviesAsync();
        Task<ApiResponseDto<MovieDto?>> GetMovieByIdAsync(Guid id);
        Task<ApiResponseDto<bool>> UpdateMovieAsync(Guid id, UpdateMovieDto command);
        Task<ApiResponseDto<bool>> DeleteMovieAsync(Guid id);
    }
}

namespace Mindflow_Web_API.DTOs
{
    public record CreateMovieDto(string Title, string Genre, DateTimeOffset ReleaseDate, double Rating);
    public record UpdateMovieDto(string Title, string Genre, DateTimeOffset ReleaseDate, double Rating);
    public record MovieDto(Guid Id, string Title, string Genre, DateTimeOffset ReleaseDate, double Rating);

}

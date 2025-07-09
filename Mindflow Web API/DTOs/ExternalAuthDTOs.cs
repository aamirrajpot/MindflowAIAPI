namespace Mindflow_Web_API.DTOs
{
    public record GoogleAuthDto(string IdToken);
    public record ExternalAuthResponseDto(string access_token, string token_type, int expires_in, bool isNewUser);
} 
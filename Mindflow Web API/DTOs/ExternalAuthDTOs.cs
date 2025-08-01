namespace Mindflow_Web_API.DTOs
{
    public record GoogleAuthDto(string IdToken);
    public record AppleAuthDto(string IdToken);
    public record ExternalAuthResponseDto(string access_token, string token_type, int expires_in, bool isNewUser);
    public record AppleKey(string Kty, string Kid, string Use, string Alg, string N, string E);
    public record AppleKeys(List<AppleKey> Keys);
} 
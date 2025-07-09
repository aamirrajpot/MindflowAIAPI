namespace Mindflow_Web_API.DTOs
{
    public record RegisterUserDto(string UserName, string Email, string Password, string FirstName, string LastName);
    public record SignInUserDto(string UserNameOrEmail, string Password);
    public record UserDto(Guid Id, string UserName, string Email, bool EmailConfirmed, string FirstName, string LastName, bool IsActive, DateTime? DateOfBirth);
    public record SendOtpResponseDto(string Email, bool Sent);
    public record VerifyOtpDto(Guid UserId, string Code);
    public record ChangePasswordDto(string CurrentPassword, string NewPassword);
    public record TokenResponseDto(string access_token, string token_type, int expires_in);
    public record UserProfileDto(string UserName, string Email, string? FirstName, string? LastName, DateTime? DateOfBirth);
    public record UpdateProfileDto(string? FirstName, string? LastName, DateTime? DateOfBirth);
} 
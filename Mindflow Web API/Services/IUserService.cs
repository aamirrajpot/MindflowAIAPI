using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IUserService
    {
        Task<UserDto> RegisterAsync(RegisterUserDto command);
        Task<TokenResponseDto?> SignInAsync(SignInUserDto command);
        Task<SendOtpResponseDto> SendOtpAsync(string email);
        Task<bool> VerifyOtpAsync(VerifyOtpDto command);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto command);
        Task<SendOtpResponseDto> ForgotPasswordAsync(ForgotPasswordDto command);
        Task<bool> ResetPasswordAsync(ResetPasswordDto command);
        Task<UserProfileDto?> GetProfileAsync(Guid userId);
        Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDto command);
    }
} 
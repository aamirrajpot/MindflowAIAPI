using Mindflow_Web_API.DTOs;

namespace Mindflow_Web_API.Services
{
    public interface IUserService
    {
        Task<UserDto> RegisterAsync(RegisterUserDto command);
        Task<SignInResponseDto?> SignInAsync(SignInUserDto command);
        Task<SendOtpResponseDto> SendOtpAsync(string email);
        Task<bool> VerifyOtpAsync(VerifyOtpDto command);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto command);
        Task<SendOtpResponseDto> ForgotPasswordAsync(ForgotPasswordDto command);
        Task<bool> ResetPasswordAsync(ResetPasswordDto command);
        Task<UserProfileDto?> GetProfileAsync(Guid userId);
        Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileDto command);
        Task<string> UploadProfilePictureAsync(Guid userId, IFormFile file, string baseUrl);
        Task<string> UploadProfilePictureBase64Async(Guid userId, string base64Image, string fileName, string baseUrl);
        Task<string> UploadProfilePictureFromUrlAsync(Guid userId, string imageUrl, string baseUrl);
        Task<bool> DeactivateAccountAsync(Guid userId);
        Task<RefreshTokenResponseDto?> RefreshTokenAsync(string refreshToken);
    }
} 
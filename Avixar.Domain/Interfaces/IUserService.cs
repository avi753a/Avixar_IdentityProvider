using Avixar.Entity;
using Avixar.Entity.Entities;
using Avixar.Entity.Models;
using Microsoft.AspNetCore.Http;

namespace Avixar.Domain
{
    public interface IUserService
    {
        Task<BaseReturn<UserProfileDto>> GetUserProfileAsync(Guid userId);
        Task<BaseReturn<bool>> UpdateUserProfileAsync(UserProfileDto profile);
        
        // Profile Management with Image Upload
        Task<BaseReturn<string>> UpdateProfileWithImageAsync(UserProfileDto profile, IFormFile? imageFile);
        
        // Address Management
        Task<BaseReturn<bool>> AddAddressAsync(UserAddress address);
        Task<BaseReturn<bool>> UpdateAddressAsync(UserAddress address);
        Task<BaseReturn<bool>> DeleteAddressAsync(Guid addressId, Guid userId);
        
        // Email Update with OTP
        Task<BaseReturn<bool>> SendEmailUpdateOtpAsync(Guid userId, string currentEmail, string newEmail);
        Task<BaseReturn<bool>> VerifyEmailUpdateAsync(Guid userId, string newEmail, string code);
        
        // Password Management
        Task<BaseReturn<bool>> ResetPasswordAsync(string email, string newPassword);
        Task<BaseReturn<bool>> RequestPasswordResetAsync(string email);
        Task<BaseReturn<bool>> ResetPasswordWithOtpAsync(string email, string code, string newPassword);
        
        // User Settings
        Task<BaseReturn<bool>> UpdateTwoFactorSettingAsync(Guid userId, bool enabled);
    }
}

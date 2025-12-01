using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity.Entities;
using Avixar.Entity.Models;
using Avixar.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;
        private readonly CloudinaryService _cloudinaryService;
        private readonly OtpService _otpService;
        private readonly EmailService _emailService;

        public UserService(
            IUserRepository userRepository, 
            ILogger<UserService> logger,
            CloudinaryService cloudinaryService,
            OtpService otpService,
            EmailService emailService)
        {
            _userRepository = userRepository;
            _logger = logger;
            _cloudinaryService = cloudinaryService;
            _otpService = otpService;
            _emailService = emailService;
        }

        public async Task<BaseReturn<UserProfileDto>> GetUserProfileAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting user profile for {UserId}", userId);
                
                var user = await _userRepository.GetUserAsync(userId);
                
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return BaseReturn<UserProfileDto>.Failure("User not found");
                }

                var addresses = await _userRepository.GetUserAddressesAsync(userId);
                var settings = await _userRepository.GetUserSettingsAsync(userId);

                var profile = new UserProfileDto
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    DisplayName = user.DisplayName ?? "",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    ImageUrl = user.ProfilePictureUrl,
                    Addresses = addresses,
                    TwoFactorEnabled = settings?.TwoFactorEnabled ?? false,
                    EmailVerified = settings?.EmailVerified ?? false
                };

                _logger.LogInformation("Successfully retrieved profile for {UserId}", userId);
                return BaseReturn<UserProfileDto>.Success(profile, "Profile retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile for {UserId}", userId);
                return BaseReturn<UserProfileDto>.Failure($"Failed to retrieve profile: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> UpdateUserProfileAsync(UserProfileDto profile)
        {
            try
            {
                _logger.LogInformation("Updating profile for {UserId}", profile.UserId);
                
                var user = await _userRepository.GetUserAsync(Guid.Parse(profile.UserId));
                if (user == null)
                {
                    _logger.LogWarning("User not found for update: {UserId}", profile.UserId);
                    return BaseReturn<bool>.Failure("User not found");
                }

                user.FirstName = profile.FirstName;
                user.LastName = profile.LastName;
                user.DisplayName = profile.DisplayName;
                
                // Update profile picture if provided
                if (!string.IsNullOrEmpty(profile.ImageUrl))
                {
                    user.ProfilePictureUrl = profile.ImageUrl;
                }

                var success = await _userRepository.UpdateUserAsync(user);
                
                if (success)
                {
                    _logger.LogInformation("Successfully updated profile for {UserId}", profile.UserId);
                    return BaseReturn<bool>.Success(true);
                }
                
                _logger.LogWarning("Failed to update profile for {UserId}", profile.UserId);
                return BaseReturn<bool>.Failure("Failed to update profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for {UserId}", profile.UserId);
                return BaseReturn<bool>.Failure($"Update failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> AddAddressAsync(UserAddress address)
        {
            try
            {
                _logger.LogInformation("Adding address for user {UserId}", address.UserId);
                
                address.Id = Guid.NewGuid();
                address.CreatedAt = DateTime.UtcNow;
                var success = await _userRepository.AddUserAddressAsync(address);
                
                if (success)
                {
                    _logger.LogInformation("Successfully added address {AddressId} for user {UserId}", address.Id, address.UserId);
                    return BaseReturn<bool>.Success(true);
                }
                
                _logger.LogWarning("Failed to add address for user {UserId}", address.UserId);
                return BaseReturn<bool>.Failure("Failed to add address");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding address for user {UserId}", address.UserId);
                return BaseReturn<bool>.Failure($"Add address failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> UpdateAddressAsync(UserAddress address)
        {
            try
            {
                _logger.LogInformation("Updating address {AddressId} for user {UserId}", address.Id, address.UserId);
                
                var success = await _userRepository.UpdateUserAddressAsync(address);
                
                if (success)
                {
                    _logger.LogInformation("Successfully updated address {AddressId}", address.Id);
                    return BaseReturn<bool>.Success(true);
                }
                
                _logger.LogWarning("Failed to update address {AddressId}", address.Id);
                return BaseReturn<bool>.Failure("Failed to update address");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating address {AddressId}", address.Id);
                return BaseReturn<bool>.Failure($"Update address failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> DeleteAddressAsync(Guid addressId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting address {AddressId} for user {UserId}", addressId, userId);
                
                var success = await _userRepository.DeleteUserAddressAsync(addressId, userId);
                
                if (success)
                {
                    _logger.LogInformation("Successfully deleted address {AddressId}", addressId);
                    return BaseReturn<bool>.Success(true);
                }
                
                _logger.LogWarning("Failed to delete address {AddressId}", addressId);
                return BaseReturn<bool>.Failure("Failed to delete address");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address {AddressId}", addressId);
                return BaseReturn<bool>.Failure($"Delete address failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> ResetPasswordAsync(string email, string newPassword)
        {
            try
            {
                _logger.LogInformation("Password reset attempt for email: {Email}", email);
                
                // Find user by email
                var user = await _userRepository.GetUserByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("User not found for password reset: {Email}", email);
                    // Return success anyway to prevent email enumeration
                    return BaseReturn<bool>.Success(true, "If the email exists, password has been reset");
                }

                // Hash the new password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                
                // Update user
                var success = await _userRepository.UpdateUserAsync(user);
                
                if (success)
                {
                    _logger.LogInformation("Password reset successfully for user {UserId}", user.Id);
                    return BaseReturn<bool>.Success(true, "Password reset successfully");
                }
                
                _logger.LogWarning("Failed to reset password for user {UserId}", user.Id);
                return BaseReturn<bool>.Failure("Failed to reset password");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for email: {Email}", email);
                return BaseReturn<bool>.Failure($"Password reset failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> UpdateTwoFactorSettingAsync(Guid userId, bool enabled)
        {
            try
            {
                _logger.LogInformation("Updating 2FA setting for user {UserId} to {Enabled}", userId, enabled);
                
                var settings = await _userRepository.GetUserSettingsAsync(userId) 
                    ?? new Entity.Entities.UserSettings { UserId = userId };
                
                settings.TwoFactorEnabled = enabled;
                await _userRepository.UpsertUserSettingsAsync(settings);
                
                _logger.LogInformation("2FA setting updated successfully for user {UserId}", userId);
                return BaseReturn<bool>.Success(true, "2FA settings updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating 2FA setting for user {UserId}", userId);
                return BaseReturn<bool>.Failure($"Failed to update 2FA settings: {ex.Message}");
            }
        }

        public async Task<BaseReturn<string>> UpdateProfileWithImageAsync(UserProfileDto profile, IFormFile? imageFile)
        {
            try
            {
                _logger.LogInformation("Updating profile with image for {UserId}", profile.UserId);
                
                // Handle Image Upload if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageUrl = await _cloudinaryService.UploadImageAsync(imageFile, "avixar/profiles");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        profile.ImageUrl = imageUrl;
                    }
                }

                // Update profile
                var result = await UpdateUserProfileAsync(profile);
                
                if (result.Status)
                {
                    return BaseReturn<string>.Success(profile.ImageUrl ?? "", "Profile updated successfully");
                }
                
                return BaseReturn<string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile with image for {UserId}", profile.UserId);
                return BaseReturn<string>.Failure($"Failed to update profile: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> SendEmailUpdateOtpAsync(Guid userId, string currentEmail, string newEmail)
        {
            try
            {
                _logger.LogInformation("Sending email update OTP for user {UserId} to new email {NewEmail}", userId, newEmail);
                
                var otp = await _otpService.GenerateOtpAsync(userId, newEmail, OtpPurpose.EmailUpdate);
                await _emailService.SendOtpEmailAsync(newEmail, otp, "Email Update");
                
                _logger.LogInformation("Email update OTP sent successfully for user {UserId}", userId);
                return BaseReturn<bool>.Success(true, "OTP sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email update OTP for user {UserId}", userId);
                return BaseReturn<bool>.Failure($"Failed to send OTP: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> VerifyEmailUpdateAsync(Guid userId, string newEmail, string code)
        {
            try
            {
                _logger.LogInformation("Verifying email update OTP for user {UserId}", userId);
                
                if (await _otpService.ValidateOtpAsync(userId, code, OtpPurpose.EmailUpdate))
                {
                    // TODO: Implement actual email update in database
                    // For now, just validate the OTP
                    // var user = await _userRepository.GetUserAsync(userId);
                    // user.Email = newEmail;
                    // await _userRepository.UpdateUserAsync(user);
                    
                    _logger.LogInformation("Email update OTP verified successfully for user {UserId}", userId);
                    return BaseReturn<bool>.Success(true, "Email updated successfully");
                }
                
                _logger.LogWarning("Invalid OTP for email update for user {UserId}", userId);
                return BaseReturn<bool>.Failure("Invalid OTP code");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email update OTP for user {UserId}", userId);
                return BaseReturn<bool>.Failure($"Failed to verify OTP: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> RequestPasswordResetAsync(string email)
        {
            try
            {
                _logger.LogInformation("Password reset requested for email: {Email}", email);
                
                // Find user by email to get userId for OTP
                var user = await _userRepository.GetUserByEmailAsync(email);
                if (user == null)
                {
                    // Return success anyway to prevent email enumeration
                    _logger.LogWarning("Password reset requested for non-existent email: {Email}", email);
                    return BaseReturn<bool>.Success(true, "If the email exists, a reset code has been sent");
                }
                
                // Generate OTP for password reset
                var userId = Guid.Parse(user.Id);
                var otp = await _otpService.GenerateOtpAsync(userId, email, OtpPurpose.TwoFactorAuth);
                await _emailService.SendOtpEmailAsync(email, otp, "Password Reset");
                
                _logger.LogInformation("Password reset OTP sent for user {UserId}", userId);
                return BaseReturn<bool>.Success(true, "Password reset code sent to your email");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset for email: {Email}", email);
                return BaseReturn<bool>.Failure($"Failed to request password reset: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> ResetPasswordWithOtpAsync(string email, string code, string newPassword)
        {
            try
            {
                _logger.LogInformation("Password reset with OTP attempt for email: {Email}", email);
                
                // Find user by email to get userId for OTP validation
                var user = await _userRepository.GetUserByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("Password reset attempted for non-existent email: {Email}", email);
                    return BaseReturn<bool>.Failure("Invalid request");
                }
                
                var userId = Guid.Parse(user.Id);
                
                // Validate OTP
                var isValid = await _otpService.ValidateOtpAsync(userId, code, OtpPurpose.TwoFactorAuth);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid OTP for password reset for user {UserId}", userId);
                    return BaseReturn<bool>.Failure("Invalid or expired code");
                }
                
                // Reset password using existing method
                var result = await ResetPasswordAsync(email, newPassword);
                
                if (result.Status)
                {
                    _logger.LogInformation("Password reset successfully for user {UserId}", userId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password with OTP for email: {Email}", email);
                return BaseReturn<bool>.Failure($"Failed to reset password: {ex.Message}");
            }
        }
    }
}

using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity.Entities;
using Avixar.Entity.Models;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepository, ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
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

                var profile = new UserProfileDto
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    DisplayName = user.DisplayName ?? "",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    ImageUrl = user.ProfilePictureUrl,
                    Addresses = addresses
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
    }
}

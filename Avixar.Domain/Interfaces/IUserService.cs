using Avixar.Entity;
using Avixar.Entity.Entities;
using Avixar.Entity.Models;

namespace Avixar.Domain
{
    public interface IUserService
    {
        Task<BaseReturn<UserProfileDto>> GetUserProfileAsync(Guid userId);
        Task<BaseReturn<bool>> UpdateUserProfileAsync(UserProfileDto profile);
        
        // Address Management
        Task<BaseReturn<bool>> AddAddressAsync(UserAddress address);
        Task<BaseReturn<bool>> UpdateAddressAsync(UserAddress address);
        Task<BaseReturn<bool>> DeleteAddressAsync(Guid addressId, Guid userId);
    }
}

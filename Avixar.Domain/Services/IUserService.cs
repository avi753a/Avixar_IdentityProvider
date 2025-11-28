using Avixar.Domain.DTOs;

namespace Avixar.Domain.Services
{
    public interface IUserService
    {
        Task<UserProfileDto?> GetUserProfileAsync(string userId);
    }
}

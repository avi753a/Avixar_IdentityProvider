using Avixar.Domain.Interfaces;
using Avixar.Domain.DTOs;

namespace Avixar.Domain.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(string userId)
        {
            if (!Guid.TryParse(userId, out var guid))
            {
                return null;
            }

            var user = await _userRepository.GetUserWithWalletAsync(guid);
            
            if (user == null)
            {
                return null;
            }

            return new UserProfileDto
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                DisplayName = user.DisplayName ?? "",
                WalletBalance = user.Wallet?.Balance ?? 0,
                Currency = user.Wallet?.Currency ?? "USD"
            };
        }
    }
}

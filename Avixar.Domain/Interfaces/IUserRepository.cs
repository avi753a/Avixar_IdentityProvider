using Avixar.Domain.DTOs;
using Avixar.Entity;

namespace Avixar.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<Guid> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl);
        Task<(bool Success, Guid UserId, string DisplayName, string Email)> LoginLocalAsync(string email, string password);
        Task<Guid> RegisterLocalAsync(string email, string password, string displayName);
        Task<ApplicationUser?> GetUserWithWalletAsync(Guid userId);
    }
}

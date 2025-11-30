using Avixar.Entity;
using Avixar.Entity.Models;

namespace Avixar.Domain
{
    public interface IAuthService
    {
        Task<BaseReturn<LoginResult>> RegisterAsync(RegisterDto dto);
        Task<BaseReturn<LoginResult>> LoginAsync(LoginDto dto);
        Task<BaseReturn<LoginResult>> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl);
    }
}

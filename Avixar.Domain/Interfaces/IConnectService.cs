using Avixar.Entity;
using Avixar.Entity.Models;
using System.Security.Claims;

namespace Avixar.Domain
{
    public interface IConnectService
    {
        Task<BaseReturn<string>> AuthorizeAsync(ExternalLoginRequest request, ClaimsPrincipal user);
        Task<BaseReturn<TokenResponse>> ExchangeTokenAsync(string clientId, string clientSecret, string code, string redirectUri);
        Task<BaseReturn<UserProfileDto>> GetUserInfoAsync(string userId);
        Task<bool> ValidateLogoutUriAsync(string clientId, string logoutUri);
    }
}

using Avixar.Domain.DTOs;
using Avixar.Entity;

namespace Avixar.Domain.Services
{
    public interface IAuthService
    {
        Task<(bool Succeeded, string[] Errors, string? Token)> RegisterAsync(RegisterDto dto);
        Task<(bool Succeeded, string? Token)> LoginAsync(LoginDto dto);
        Task<string> GenerateJwtTokenAsync(ApplicationUser user);
    }
}

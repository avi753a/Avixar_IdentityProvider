using Avixar.Entity;
using Microsoft.AspNetCore.Identity;

namespace Avixar.Domain
{
    public class AuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<(bool Succeeded, string[] Errors)> RegisterAsync(string email, string password, string displayName)
        {
            var user = new ApplicationUser 
            { 
                UserName = email, 
                Email = email,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Create Wallet
                user.Wallet = new Wallet { UserId = user.Id, Balance = 0 };
                await _userManager.UpdateAsync(user); // Or use a separate repository to add wallet
                
                return (true, Array.Empty<string>());
            }

            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
            return result.Succeeded;
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}

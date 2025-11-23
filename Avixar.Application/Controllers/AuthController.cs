using Avixar.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Avixar.Entity;
using System.Security.Claims;

namespace Avixar.Application.Controllers
{
    public class AuthController : Controller
    {
        private readonly AuthService _authService;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(AuthService authService, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _authService = authService;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (await _authService.LoginAsync(email, password))
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password, string displayName)
        {
            var (succeeded, errors) = await _authService.RegisterAsync(email, password, displayName);
            if (succeeded)
            {
                await _authService.LoginAsync(email, password);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            return View();
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
                return RedirectToAction("Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }

            // If the user does not have an account, create one
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                        CreatedAt = DateTime.UtcNow
                    };
                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return RedirectToAction("Login");
                    }
                }

                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
    }
}

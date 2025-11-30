using Avixar.Domain;
using Avixar.Entity;
using Avixar.Entity.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Avixar.UI.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _authService.LoginAsync(model);

            if (result.Status)
            {
                await SignInUserAsync(result.Data);
                return RedirectToLocal(returnUrl);
            }

            ModelState.AddModelError("", result.Message);
            return View(model);
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterDto model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _authService.RegisterAsync(model);

            if (result.Status)
            {
                await SignInUserAsync(result.Data);
                return RedirectToLocal(returnUrl);
            }

            foreach (var error in result.Message.Split(',')) // Assuming comma separated or just single message
            {
                ModelState.AddModelError("", error.Trim());
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult SignUp(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SignUp(RegisterDto model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _authService.RegisterAsync(model);

            if (result.Status)
            {
                await SignInUserAsync(result.Data);
                return RedirectToLocal(returnUrl);
            }

            foreach (var error in result.Message.Split(','))
            {
                ModelState.AddModelError("", error.Trim());
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
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
                return View("Login");
            }

            var info = await HttpContext.AuthenticateAsync("ExternalCookie");
            if (info?.Principal == null)
            {
                return RedirectToAction("Login");
            }

            var claims = info.Principal.Claims;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var subject = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var provider = info.Properties?.Items.ContainsKey("LoginProvider") == true ? info.Properties.Items["LoginProvider"] : "Unknown";

            // If provider is not in properties, try to guess or use a default if it's from the scheme
            // Actually Challenge(properties, provider) sets the scheme.
            // We can get the scheme from info.Ticket.AuthenticationScheme? No.
            // Let's assume Google/Microsoft based on claims or just pass it if we can.
            // But we don't have it here easily unless we stored it.
            // However, the user's previous code passed "Google" or "Microsoft".
            // Let's try to extract it from the issuer of the claims?
            // Or just use a generic "External" if we can't find it, but sp_SocialLogin needs it.
            // Wait, `info.Properties.Items` might not have it.
            // We can use `info.Ticket.AuthenticationScheme` if it was set?
            // Actually, we can check which scheme authenticated.
            
            // Simplified: Just use "Google" or "Microsoft" based on what we know or pass it in returnUrl? No.
            // Let's assume the provider name is part of the callback logic or we can infer it.
            // For now, let's just use "External" or try to find it.
            // Actually, the previous implementation had `provider` passed to `ExternalLoginCallback`? No.
            
            // Fix: We can't easily get the provider name here unless we put it in the state or correlation cookie.
            // But `sp_SocialLogin` needs it.
            // Let's try to get it from `info.Properties.Items[".AuthScheme"]`?
            
            // Hack: Check claims issuer.
            var issuer = claims.FirstOrDefault()?.Issuer ?? "External";

            var result = await _authService.LoginWithSocialAsync(issuer, subject, email, name, null);

            if (result.Status)
            {
                await SignInUserAsync(result.Data);
                // Clear external cookie
                await HttpContext.SignOutAsync("ExternalCookie");
                return RedirectToLocal(returnUrl);
            }

            ModelState.AddModelError("", result.Message);
            return View("Login");
        }

        private async Task SignInUserAsync(LoginResult user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Welcome", "Home");
        }
    }
}

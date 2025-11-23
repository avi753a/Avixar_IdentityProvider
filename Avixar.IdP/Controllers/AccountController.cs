using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Avixar.IdP.Models;
using Avixar.IdP.Services;
using System.Security.Claims;

namespace Avixar.IdP.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthService _authService;

        public AccountController(AuthService authService)
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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (ModelState.IsValid)
            {
                if (await _authService.ValidateUserAsync(model.Email, model.Password)) 
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, model.Email),
                        new Claim(ClaimTypes.Role, "Admin")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
                
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return View(model);
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl, provider });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string provider, string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
                return View("Login");
            }

            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return RedirectToAction("Login");
            }

            var claims = result.Principal.Claims;
            var subjectId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value 
                            ?? claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value 
                       ?? claims.FirstOrDefault(c => c.Type == "urn:github:name")?.Value; // GitHub specific?
            
            // Try to get avatar
            var pic = claims.FirstOrDefault(c => c.Type == "urn:google:picture")?.Value 
                      ?? claims.FirstOrDefault(c => c.Type == "avatar_url")?.Value; // GitHub

            if (subjectId == null)
            {
                 ModelState.AddModelError(string.Empty, "Could not retrieve user ID from external provider.");
                 return View("Login");
            }

            try
            {
                // Call DB Procedure
                var userId = await _authService.SocialLoginAsync(provider, subjectId, email, name, pic);

                // Create App Principal
                var appClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, email ?? name ?? "User"),
                    new Claim(ClaimTypes.Role, "User")
                };

                var appIdentity = new ClaimsIdentity(appClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Re-sign in with App Identity
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(appIdentity));

                // Show Welcome View
                ViewData["Username"] = name ?? email;
                ViewData["ReturnUrl"] = returnUrl;
                return View("Welcome");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Login failed: {ex.Message}");
                return View("Login");
            }
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}

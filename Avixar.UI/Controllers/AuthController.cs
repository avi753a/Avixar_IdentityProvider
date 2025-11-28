using Avixar.Domain.DTOs;
using Avixar.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using System.Security.Claims;

namespace Avixar.UI.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserRepository _userRepository;

        public AuthController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, userId, displayName, email) = await _userRepository.LoginLocalAsync(model.Email, model.Password);
            
            if (success)
            {
                await SignInUserAsync(userId, email, displayName, "LOCAL");
                return RedirectToAction("Welcome", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View("SignUp");
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
                return View("SignUp", model);

            try
            {
                var userId = await _userRepository.RegisterLocalAsync(model.Email, model.Password, model.DisplayName);
                await SignInUserAsync(userId, model.Email, model.DisplayName, "LOCAL");
                return RedirectToAction("Welcome", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("SignUp", model);
            }
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            
            // Map provider name to scheme
            string scheme = provider switch
            {
                "Google" => GoogleDefaults.AuthenticationScheme,
                "Microsoft" => MicrosoftAccountDefaults.AuthenticationScheme,
                _ => throw new ArgumentException("Invalid provider")
            };

            return Challenge(properties, scheme);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            // Check for remote errors first
            if (remoteError != null)
            {
                ViewBag.ErrorTitle = "External Authentication Error";
                ViewBag.ErrorMessage = $"Error from external provider: {remoteError}";
                ViewBag.ErrorDetails = "The external authentication provider returned an error. Please try again or contact support.";
                return View("Error");
            }

            try
            {
                // Authenticate from the external cookie scheme
                var externalAuth = await HttpContext.AuthenticateAsync("ExternalCookie");
                
                if (!externalAuth.Succeeded)
                {
                    ViewBag.ErrorTitle = "Authentication Failed";
                    ViewBag.ErrorMessage = "External authentication was not successful.";
                    ViewBag.ErrorDetails = "The authentication process did not complete successfully. Please try again.";
                    Console.WriteLine("External auth failed - externalAuth.Succeeded = false");
                    return View("Error");
                }

                // Extract claims
                var claims = externalAuth.Principal.Claims.ToList();
                
                // Log claims for debugging
                Console.WriteLine("=== External Login Claims ===");
                foreach (var claim in claims)
                {
                    Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
                }
                Console.WriteLine("=== End Claims ===");

                var subjectId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                
                // Determine provider from issuer or claims
                var provider = "GOOGLE";
                var issuer = claims.FirstOrDefault()?.Issuer;
                
                if (issuer?.Contains("google", StringComparison.OrdinalIgnoreCase) == true)
                {
                    provider = "GOOGLE";
                }
                else if (issuer?.Contains("microsoft", StringComparison.OrdinalIgnoreCase) == true || 
                         issuer?.Contains("login.microsoftonline", StringComparison.OrdinalIgnoreCase) == true)
                {
                    provider = "MICROSOFT";
                }

                Console.WriteLine($"Detected Provider: {provider}");
                Console.WriteLine($"SubjectId: {subjectId}");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Name: {name}");

                if (string.IsNullOrEmpty(subjectId))
                {
                    ViewBag.ErrorTitle = "Missing User ID";
                    ViewBag.ErrorMessage = "Could not retrieve user ID from external provider.";
                    ViewBag.ErrorDetails = $"Provider: {provider}, Claims received: {claims.Count}";
                    return View("Error");
                }

                if (string.IsNullOrEmpty(email))
                {
                    ViewBag.ErrorTitle = "Missing Email";
                    ViewBag.ErrorMessage = "Could not retrieve email from external provider.";
                    ViewBag.ErrorDetails = $"Provider: {provider}, SubjectId: {subjectId}";
                    return View("Error");
                }

                Console.WriteLine($"Calling LoginWithSocialAsync: provider={provider}, subjectId={subjectId}, email={email}");

                // Sync with database
                var userId = await _userRepository.LoginWithSocialAsync(
                    provider, 
                    subjectId, 
                    email, 
                    name ?? email, 
                    null
                );

                Console.WriteLine($"LoginWithSocialAsync returned userId: {userId}");

                // Sign out of external cookie
                await HttpContext.SignOutAsync("ExternalCookie");

                // Sign in to main application cookie
                await SignInUserAsync(userId, email, name ?? email, provider);

                Console.WriteLine("User signed in successfully, redirecting to Welcome");

                return RedirectToAction("Welcome", "Home");
            }
            catch (Exception ex)
            {
                // Log the full exception
                Console.WriteLine($"=== External Login Exception ===");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Stack: {ex.InnerException.StackTrace}");
                }
                Console.WriteLine($"=== End Exception ===");
                
                ViewBag.ErrorTitle = "Login Error";
                ViewBag.ErrorMessage = ex.Message;
                ViewBag.ErrorDetails = $"Stack trace: {ex.StackTrace}";
                return View("Error");
            }
        }

        private async Task SignInUserAsync(Guid userId, string email, string displayName, string provider)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Email, email),
                new Claim("Provider", provider)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Welcome", "Home");
        }
    }
}

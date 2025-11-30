using Avixar.Domain;
using Avixar.Entity;
using Avixar.Entity.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Avixar.UI.Controllers
{
    [ApiController]
    public class ExternalConnectController : ControllerBase
    {
        private readonly IConnectService _connectService;

        public ExternalConnectController(IConnectService connectService)
        {
            _connectService = connectService;
        }

        [AllowAnonymous]
        [HttpGet("connect/authorize")]
        public async Task<IActionResult> Authorize([FromQuery] ExternalLoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _connectService.AuthorizeAsync(request, User);

            if (result.Status)
            {
                // Success: Redirect back to client with code
                return Redirect(result.Data);
            }
            else if (result.Message == "NotAuthenticated")
            {
                // User not logged in: Trigger Login UI
                // We need to preserve the return URL so after login they come back here
                var properties = new AuthenticationProperties
                {
                    RedirectUri = Request.Path + Request.QueryString
                };
                return Challenge(properties, CookieAuthenticationDefaults.AuthenticationScheme);
            }
            else
            {
                // Other errors (invalid client, etc.)
                return BadRequest(new { error = result.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("connect/token")]
        public async Task<IActionResult> Token([FromForm] string client_id, [FromForm] string client_secret, [FromForm] string code, [FromForm] string redirect_uri, [FromForm] string grant_type)
        {
            if (grant_type != "authorization_code")
                return BadRequest(new { error = "unsupported_grant_type" });

            var result = await _connectService.ExchangeTokenAsync(client_id, client_secret, code, redirect_uri);

            if (result.Status)
            {
                return Ok(result.Data);
            }

            return BadRequest(new { error = "invalid_grant", error_description = result.Message });
        }

        [Authorize]
        [HttpGet("connect/userinfo")]
        public async Task<IActionResult> UserInfo()
        {
            // Get UserId from JWT claims
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _connectService.GetUserInfoAsync(userId);

            if (result.Status)
            {
                return Ok(new
                {
                    sub = result.Data.UserId,
                    name = result.Data.DisplayName,
                    email = result.Data.Email,
                    picture = result.Data.ImageUrl
                });
            }

            return BadRequest(result.Message);
        }

        [HttpGet("connect/logout")]
        public async Task<IActionResult> Logout([FromQuery] string post_logout_redirect_uri, [FromQuery] string? client_id = null)
        {
            try
            {
                // Clear authentication cookie
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Validate redirect URI if client_id is provided
                if (!string.IsNullOrEmpty(client_id) && !string.IsNullOrEmpty(post_logout_redirect_uri))
                {
                    var isValid = await _connectService.ValidateLogoutUriAsync(client_id, post_logout_redirect_uri);
                    if (isValid)
                    {
                        return Redirect(post_logout_redirect_uri);
                    }
                }

                // If no valid redirect, go to a default logout page or home
                return Redirect(post_logout_redirect_uri ?? "/");
            }
            catch
            {
                return Redirect("/");
            }
        }
    }
}

using Avixar.Domain.DTOs;
using Avixar.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Avixar.Application.Controllers.Api
{
    [ApiController]
    [Route("api/connect")]
    public class ExternalConnectController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;

        public ExternalConnectController(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        /// <summary>
        /// Generates a JWT token for third-party applications (Game/Shop)
        /// </summary>
        /// <param name="loginDto">Login credentials</param>
        /// <returns>JWT token if successful</returns>
        [HttpPost("token")]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetToken([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (succeeded, token) = await _authService.LoginAsync(loginDto);

            if (!succeeded || token == null)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            return Ok(new TokenResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 3600 // 1 hour
            });
        }

        /// <summary>
        /// Returns user profile information for authenticated third-party apps
        /// </summary>
        /// <returns>User profile with wallet balance</returns>
        [HttpGet("user-info")]
        [Authorize]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserInfo()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var userProfile = await _userService.GetUserProfileAsync(userId);

            if (userProfile == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(userProfile);
        }
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
    }
}

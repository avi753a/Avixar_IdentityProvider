using Microsoft.AspNetCore.Mvc;
using Avixar.IdP.Models;
using Avixar.IdP.Services;

namespace Avixar.IdP.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (await _authService.ValidateUserAsync(model.Email, model.Password))
            {
                // In a real API scenario, we might return a JWT token here
                return Ok(new { message = "Login successful", user = model.Email });
            }
            return Unauthorized(new { message = "Invalid credentials" });
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { message = "Pong", time = DateTime.UtcNow });
        }
    }
}

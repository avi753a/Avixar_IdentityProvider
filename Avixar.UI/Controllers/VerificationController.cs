using Avixar.Data;
using Avixar.Domain;
using Avixar.Entity.Entities;
using Avixar.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Avixar.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(
            IVerificationService verificationService,
            ILogger<VerificationController> logger)
        {
            _verificationService = verificationService;
            _logger = logger;
        }

        /// <summary>
        /// Send verification email with link (for signup)
        /// POST /api/verification/send-email
        /// </summary>
        [HttpPost("send-email")]
        [Authorize]
        public async Task<IActionResult> SendVerificationEmail()
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(email))
                {
                    return BadRequest(new { message = "User information not found" });
                }

                var userId = Guid.Parse(userIdStr);
                
                // Base URL for verification link
                var baseUrl = $"{Request.Scheme}://{Request.Host}/api/verification/verify-email";

                var result = await _verificationService.SendVerificationEmailAsync(userId, email, baseUrl);
                
                if (result.Status)
                    return Ok(new { message = result.Message });
                    
                return StatusCode(500, new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Verify email with token (link from email)
        /// GET /api/verification/verify-email?token={token}
        /// </summary>
        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { message = "Token is required" });
                }

                var result = await _verificationService.VerifyEmailWithTokenAsync(token);

                if (result.Status)
                {
                    // Redirect to success page or return success message
                    return Redirect("/Home/Welcome?emailVerified=true");
                }

                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Send OTP for 2FA or email update
        /// POST /api/verification/send-otp
        /// Body: { "purpose": "TwoFactorAuth" | "EmailUpdate" }
        /// </summary>
        [HttpPost("send-otp")]
        [Authorize]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(email))
                {
                    return BadRequest(new { message = "User information not found" });
                }

                var userId = Guid.Parse(userIdStr);

                // Parse purpose
                if (!Enum.TryParse<OtpPurpose>(request.Purpose, true, out var purpose))
                {
                    return BadRequest(new { message = "Invalid purpose. Use: TwoFactorAuth or EmailUpdate" });
                }

                // Use custom email if provided (for email update)
                var targetEmail = !string.IsNullOrEmpty(request.Email) ? request.Email : email;

                var result = await _verificationService.SendOtpAsync(userId, targetEmail, purpose, request.ExpirySeconds);
                
                if (result.Status)
                {
                    return Ok(new 
                    { 
                        message = result.Message,
                        expiresIn = request.ExpirySeconds ?? 300
                    });
                }
                
                return StatusCode(500, new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Validate OTP
        /// POST /api/verification/validate-otp
        /// Body: { "code": "123456", "purpose": "TwoFactorAuth" | "EmailUpdate" }
        /// </summary>
        [HttpPost("validate-otp")]
        [Authorize]
        public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpRequest request)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdStr))
                {
                    return BadRequest(new { message = "User information not found" });
                }

                var userId = Guid.Parse(userIdStr);

                // Parse purpose
                if (!Enum.TryParse<OtpPurpose>(request.Purpose, true, out var purpose))
                {
                    return BadRequest(new { message = "Invalid purpose" });
                }

                var result = await _verificationService.ValidateOtpAsync(userId, request.Code, purpose);

                if (result.Status)
                {
                    return Ok(new { message = result.Message, valid = true });
                }

                // Get remaining attempts
                var attemptsResult = await _verificationService.GetRemainingAttemptsAsync(userId, purpose);
                var remainingAttempts = attemptsResult.Status ? attemptsResult.Data : 0;

                return BadRequest(new 
                { 
                    message = result.Message,
                    valid = false,
                    remainingAttempts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating OTP");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Check if user has valid OTP
        /// GET /api/verification/has-otp?purpose={purpose}
        /// </summary>
        [HttpGet("has-otp")]
        [Authorize]
        public async Task<IActionResult> HasValidOtp([FromQuery] string purpose)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdStr))
                {
                    return BadRequest(new { message = "User information not found" });
                }

                var userId = Guid.Parse(userIdStr);

                if (!Enum.TryParse<OtpPurpose>(purpose, true, out var otpPurpose))
                {
                    return BadRequest(new { message = "Invalid purpose" });
                }

                var result = await _verificationService.HasValidOtpAsync(userId, otpPurpose);

                return Ok(new { hasValidOtp = result.Data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OTP validity");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }
    }

    // Request models
    public class SendOtpRequest
    {
        public string Purpose { get; set; } = string.Empty; // TwoFactorAuth, EmailUpdate
        public string? Email { get; set; } // For email update, send to new email
        public int? ExpirySeconds { get; set; } // Custom expiry (1-31536000)
    }

    public class ValidateOtpRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }
}

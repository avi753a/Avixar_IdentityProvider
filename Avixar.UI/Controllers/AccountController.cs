using Avixar.Domain;
using Avixar.Entity;
using Avixar.Entity.Entities;
using Avixar.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Avixar.UI.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IUserService userService,
            ILogger<AccountController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Account page accessed by user: {UserId}", userId);
                
                if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Auth");

                var result = await _userService.GetUserProfileAsync(Guid.Parse(userId));
                if (!result.Status) return RedirectToAction("Login", "Auth");

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account page");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(UserProfileDto model)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Profile update attempt by user: {UserId}", userId);
                
                // Remove optional fields from ModelState validation
                ModelState.Remove("ProfileImage");
                ModelState.Remove("TwoFactorEnabled");
                ModelState.Remove("EmailVerified");
                ModelState.Remove("Addresses");
                
                if (!ModelState.IsValid) return View("Index", model);

                model.UserId = userId;

                // Use service method that handles image upload and profile update
                var result = await _userService.UpdateProfileWithImageAsync(model, model.ProfileImage);
                
                if (result.Status)
                    TempData["Message"] = "Profile updated successfully";
                else
                    TempData["Error"] = result.Message;

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                TempData["Error"] = "An error occurred";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress(UserAddress address)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Add address attempt by user: {UserId}", userId);
                
                address.UserId = Guid.Parse(userId);
                var result = await _userService.AddAddressAsync(address);

                if (result.Status)
                    TempData["Message"] = "Address added successfully";
                else
                    TempData["Error"] = result.Message;

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding address");
                TempData["Error"] = "An error occurred";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddress(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Delete address attempt by user: {UserId}, AddressId: {AddressId}", userId, id);
                
                var result = await _userService.DeleteAddressAsync(id, Guid.Parse(userId));

                if (result.Status)
                    TempData["Message"] = "Address deleted successfully";
                else
                    TempData["Error"] = result.Message;

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address: {AddressId}", id);
                TempData["Error"] = "An error occurred";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendEmailUpdateOtp(string newEmail)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var currentEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                
                var result = await _userService.SendEmailUpdateOtpAsync(userId, currentEmail, newEmail);
                
                if (result.Status)
                    return Ok(new { message = result.Message });
                    
                return StatusCode(500, new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email update OTP");
                return StatusCode(500, new { message = "Failed to send OTP" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyEmailUpdate(string newEmail, string code)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var result = await _userService.VerifyEmailUpdateAsync(userId, newEmail, code);
                
                if (result.Status)
                    return Ok(new { message = result.Message });
                    
                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email update OTP");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTwoFactor(bool enabled)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var result = await _userService.UpdateTwoFactorSettingAsync(userId, enabled);
                
                if (result.Status)
                    return Ok(new { message = result.Message });
                    
                return StatusCode(500, new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling 2FA");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RequestPasswordReset(string email)
        {
            try
            {
                var result = await _userService.RequestPasswordResetAsync(email);
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string email, string code, string newPassword)
        {
            try
            {
                var result = await _userService.ResetPasswordWithOtpAsync(email, code, newPassword);
                
                if (result.Status)
                    return Ok(new { message = result.Message });
                    
                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }
    }
}

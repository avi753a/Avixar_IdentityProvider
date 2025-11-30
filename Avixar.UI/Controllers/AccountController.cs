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

        public AccountController(IUserService userService, ILogger<AccountController> logger)
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
                
                if (!ModelState.IsValid) return View("Index", model);

                model.UserId = userId;
                var result = await _userService.UpdateUserProfileAsync(model);
                
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
    }
}

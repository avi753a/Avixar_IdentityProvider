using Avixar.Entity.Entities;

namespace Avixar.Entity
{
    public class UserProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ImageUrl { get; set; } = string.Empty;
        public Microsoft.AspNetCore.Http.IFormFile? ProfileImage { get; set; }
        
        public List<UserAddress>? Addresses { get; set; } = new List<UserAddress>();
        
        // Security Settings
        public bool? TwoFactorEnabled { get; set; }
        public bool? EmailVerified { get; set; }
    }
}

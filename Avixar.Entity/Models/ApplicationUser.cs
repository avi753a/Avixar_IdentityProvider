using Avixar.Entity;
using Microsoft.AspNetCore.Identity;

namespace Avixar.Entity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        
        // Navigation Properties
        public virtual Wallet? Wallet { get; set; }
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public virtual ICollection<Avixar.Entity.Entities.UserAddress> Addresses { get; set; } = new List<Avixar.Entity.Entities.UserAddress>();
    }
}

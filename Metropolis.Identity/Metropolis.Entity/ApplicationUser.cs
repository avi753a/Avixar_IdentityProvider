using Microsoft.AspNetCore.Identity;

namespace Metropolis.Entity
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        
        // Navigation Properties
        public virtual Wallet? Wallet { get; set; }
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}

using System.ComponentModel.DataAnnotations;

namespace Metropolis.Entity
{
    public class AuditLog
    {
        [Key]
        public Guid Id { get; set; }

        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        public string Action { get; set; } = string.Empty; // e.g., "LOGIN", "LOGOUT", "UPDATE_PROFILE"
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

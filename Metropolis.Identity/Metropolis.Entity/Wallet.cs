using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Metropolis.Entity
{
    public class Wallet
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Balance { get; set; } = 0;

        public string Currency { get; set; } = "USD";
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

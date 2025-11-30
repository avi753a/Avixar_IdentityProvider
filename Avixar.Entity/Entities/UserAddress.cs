using System.ComponentModel.DataAnnotations;

namespace Avixar.Entity.Entities
{
    public class UserAddress
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? Label { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public int? StateId { get; set; }
        public string PostalCode { get; set; } = string.Empty;
        public string? CountryIsoCode { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation Properties
        public virtual ApplicationUser? User { get; set; }
        public virtual State? State { get; set; }
        public virtual Country? Country { get; set; }
    }
}

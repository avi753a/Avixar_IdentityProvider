namespace Avixar.Entity.Entities
{
    public class UserSettings
    {
        public Guid UserId { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool EmailVerified { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public bool EmailNotifications { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

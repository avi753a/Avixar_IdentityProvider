namespace Avixar.Entity.Entities
{
    public enum OtpPurpose
    {
        EmailVerification,
        TwoFactorAuth,
        EmailUpdate
    }

    public class OtpCode
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Code { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

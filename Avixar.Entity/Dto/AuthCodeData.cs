namespace Avixar.Entity
{
    public class AuthCodeData
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string ClientId { get; set; }
        public string RedirectUri { get; set; }
        public string Nonce { get; set; } // Optional: For OIDC security
        public DateTime CreatedAt { get; set; }
    }
}

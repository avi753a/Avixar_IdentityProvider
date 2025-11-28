namespace Avixar.Domain.DTOs
{
    public class UserProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public decimal WalletBalance { get; set; }
        public string Currency { get; set; } = "USD";
    }
}

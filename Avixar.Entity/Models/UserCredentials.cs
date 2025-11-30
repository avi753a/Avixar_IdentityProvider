namespace Avixar.Entity.Models
{
    /// <summary>
    /// Represents user credentials retrieved from the database
    /// Used internally by the repository layer to return data for validation
    /// </summary>
    public class UserCredentials
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}

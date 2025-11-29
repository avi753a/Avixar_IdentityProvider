namespace Avixar.Entity
{
    public class TokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; } = "Bearer";
        public int expires_in { get; set; }
        public string id_token { get; set; } // Optional but standard
    }
}

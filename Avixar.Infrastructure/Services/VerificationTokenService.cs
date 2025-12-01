using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace Avixar.Infrastructure.Services
{
    public class VerificationTokenService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<VerificationTokenService> _logger;
        private readonly int _tokenExpiryHours;

        public VerificationTokenService(
            IDistributedCache cache,
            IConfiguration configuration,
            ILogger<VerificationTokenService> logger)
        {
            _cache = cache;
            _logger = logger;
            _tokenExpiryHours = int.Parse(configuration["EmailVerification:TokenExpiryHours"] ?? "24");
        }

        /// <summary>
        /// Generate a secure verification token and store in Redis
        /// </summary>
        public async Task<string> GenerateVerificationTokenAsync(Guid userId, string email)
        {
            try
            {
                _logger.LogInformation("Generating verification token for user {UserId}", userId);

                // Generate secure random token
                var tokenBytes = new byte[32];
                RandomNumberGenerator.Fill(tokenBytes);
                var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

                // Store token data in Redis
                var tokenData = new
                {
                    UserId = userId,
                    Email = email,
                    CreatedAt = DateTime.UtcNow
                };

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_tokenExpiryHours)
                };

                await _cache.SetStringAsync(
                    $"email_verification:{token}",
                    JsonSerializer.Serialize(tokenData),
                    options
                );

                _logger.LogInformation("Verification token generated and cached for user {UserId}", userId);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating verification token for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Validate token from Redis cache
        /// </summary>
        public async Task<(bool Success, Guid UserId, string Email)> ValidateTokenAsync(string token)
        {
            try
            {
                _logger.LogInformation("Validating verification token");

                var key = $"email_verification:{token}";
                var cachedData = await _cache.GetStringAsync(key);

                if (string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogWarning("Verification token not found or expired");
                    return (false, Guid.Empty, string.Empty);
                }

                var tokenData = JsonSerializer.Deserialize<TokenData>(cachedData);
                if (tokenData == null)
                {
                    _logger.LogWarning("Invalid token data format");
                    return (false, Guid.Empty, string.Empty);
                }

                // Remove token after validation (one-time use)
                await _cache.RemoveAsync(key);

                _logger.LogInformation("Token validated successfully for user {UserId}", tokenData.UserId);
                return (true, tokenData.UserId, tokenData.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating verification token");
                return (false, Guid.Empty, string.Empty);
            }
        }

        private class TokenData
        {
            public Guid UserId { get; set; }
            public string Email { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }
    }
}

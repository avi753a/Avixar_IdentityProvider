using Avixar.Entity.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace Avixar.Infrastructure.Services
{
    public class OtpService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<OtpService> _logger;
        private readonly int _otpExpirySeconds;
        private readonly int _maxAttempts;
        private const int MAX_EXPIRY_SECONDS = 31536000; // 1 year in seconds

        public OtpService(IConnectionMultiplexer redis, IConfiguration configuration, ILogger<OtpService> logger)
        {
            _redis = redis;
            _logger = logger;
            
            // Get expiry from config, default to 300 seconds (5 minutes), cap at 1 year
            var configExpiry = int.Parse(configuration["OtpSettings:ExpirySeconds"] ?? "300");
            _otpExpirySeconds = Math.Min(configExpiry, MAX_EXPIRY_SECONDS);
            
            _maxAttempts = int.Parse(configuration["OtpSettings:MaxAttempts"] ?? "3");
        }

        /// <summary>
        /// Generate a 6-digit OTP code and store in Redis
        /// </summary>
        public async Task<string> GenerateOtpAsync(Guid userId, string email, OtpPurpose purpose, int? customExpirySeconds = null)
        {
            try
            {
                _logger.LogInformation("Generating OTP for user {UserId}, purpose: {Purpose}", userId, purpose);

                // Generate random 6-digit code
                var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

                var db = _redis.GetDatabase();
                
                // Redis key format: otp:{userId}:{purpose}
                var otpKey = $"otp:{userId}:{purpose}";
                var attemptsKey = $"otp:attempts:{userId}:{purpose}";

                // Use custom expiry if provided, otherwise use default (capped at 1 year)
                var expirySeconds = customExpirySeconds.HasValue 
                    ? Math.Min(customExpirySeconds.Value, MAX_EXPIRY_SECONDS) 
                    : _otpExpirySeconds;

                // Store OTP with metadata as JSON
                var otpData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Code = code,
                    Email = email,
                    Purpose = purpose.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds)
                });

                await db.StringSetAsync(otpKey, otpData, TimeSpan.FromSeconds(expirySeconds));
                await db.StringSetAsync(attemptsKey, "0", TimeSpan.FromSeconds(expirySeconds));

                _logger.LogInformation("OTP generated successfully for user {UserId}, expires in {Seconds} seconds", userId, expirySeconds);
                return code;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OTP for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Validate OTP code from Redis
        /// </summary>
        public async Task<bool> ValidateOtpAsync(Guid userId, string code, OtpPurpose purpose)
        {
            try
            {
                _logger.LogInformation("Validating OTP for user {UserId}, purpose: {Purpose}", userId, purpose);

                var db = _redis.GetDatabase();
                
                var otpKey = $"otp:{userId}:{purpose}";
                var attemptsKey = $"otp:attempts:{userId}:{purpose}";

                // Get OTP data
                var otpDataJson = await db.StringGetAsync(otpKey);
                if (otpDataJson.IsNullOrEmpty)
                {
                    _logger.LogWarning("No active OTP found for user {UserId}", userId);
                    return false;
                }

                // Get attempts
                var attemptsStr = await db.StringGetAsync(attemptsKey);
                var attempts = int.Parse(attemptsStr.HasValue ? attemptsStr.ToString() : "0");

                // Check if max attempts exceeded
                if (attempts >= _maxAttempts)
                {
                    _logger.LogWarning("Max OTP attempts exceeded for user {UserId}", userId);
                    return false;
                }

                // Increment attempts
                await db.StringIncrementAsync(attemptsKey);

                // Parse OTP data
                var otpData = System.Text.Json.JsonSerializer.Deserialize<OtpData>(otpDataJson.ToString());
                
                if (otpData == null)
                {
                    _logger.LogError("Failed to deserialize OTP data for user {UserId}", userId);
                    return false;
                }

                // Validate code
                if (otpData.Code != code)
                {
                    _logger.LogWarning("Invalid OTP code for user {UserId}, attempt {Attempt}/{Max}", userId, attempts + 1, _maxAttempts);
                    return false;
                }

                // Delete OTP after successful validation
                await db.KeyDeleteAsync(otpKey);
                await db.KeyDeleteAsync(attemptsKey);

                _logger.LogInformation("OTP validated successfully for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating OTP for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Check if user has a valid OTP
        /// </summary>
        public async Task<bool> HasValidOtpAsync(Guid userId, OtpPurpose purpose)
        {
            try
            {
                var db = _redis.GetDatabase();
                var otpKey = $"otp:{userId}:{purpose}";
                
                return await db.KeyExistsAsync(otpKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OTP validity for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Get remaining attempts for OTP
        /// </summary>
        public async Task<int> GetRemainingAttemptsAsync(Guid userId, OtpPurpose purpose)
        {
            try
            {
                var db = _redis.GetDatabase();
                var attemptsKey = $"otp:attempts:{userId}:{purpose}";
                
                var attemptsStr = await db.StringGetAsync(attemptsKey);
                var attempts = int.Parse(attemptsStr.HasValue ? attemptsStr.ToString() : "0");
                
                return Math.Max(0, _maxAttempts - attempts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting remaining attempts for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Delete OTP (for manual invalidation)
        /// </summary>
        public async Task DeleteOtpAsync(Guid userId, OtpPurpose purpose)
        {
            try
            {
                var db = _redis.GetDatabase();
                var otpKey = $"otp:{userId}:{purpose}";
                var attemptsKey = $"otp:attempts:{userId}:{purpose}";
                
                await db.KeyDeleteAsync(otpKey);
                await db.KeyDeleteAsync(attemptsKey);
                
                _logger.LogInformation("OTP deleted for user {UserId}, purpose: {Purpose}", userId, purpose);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting OTP for user {UserId}", userId);
            }
        }

        // Helper class for deserializing OTP data from Redis
        private class OtpData
        {
            public string Code { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Purpose { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}

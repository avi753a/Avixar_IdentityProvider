using Microsoft.Extensions.Configuration;

namespace Avixar.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for IConfiguration to simplify access to common configuration values
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Gets the default database connection string
        /// </summary>
        /// <param name="configuration">The configuration instance</param>
        /// <returns>The connection string</returns>
        /// <exception cref="ArgumentNullException">Thrown when connection string is not found</exception>
        public static string GetDefaultConnectionString(this IConfiguration configuration)
        {
            return configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found in configuration.");
        }

        /// <summary>
        /// Gets the encryption key used for AES encryption
        /// </summary>
        /// <param name="configuration">The configuration instance</param>
        /// <returns>The encryption key</returns>
        /// <exception cref="ArgumentNullException">Thrown when encryption key is not found</exception>
        public static string GetEncryptionKey(this IConfiguration configuration)
        {
            return configuration["Security:EncryptionKey"]
                ?? throw new ArgumentNullException("Security:EncryptionKey not found in configuration.");
        }

        /// <summary>
        /// Gets the blind index key used for HMAC hashing
        /// </summary>
        /// <param name="configuration">The configuration instance</param>
        /// <returns>The blind index key</returns>
        /// <exception cref="ArgumentNullException">Thrown when blind index key is not found</exception>
        public static string GetBlindIndexKey(this IConfiguration configuration)
        {
            return configuration["Security:BlindIndexKey"]
                ?? throw new ArgumentNullException("Security:BlindIndexKey not found in configuration.");
        }

        /// <summary>
        /// Tries to get the default connection string without throwing an exception
        /// </summary>
        /// <param name="configuration">The configuration instance</param>
        /// <param name="connectionString">The connection string if found</param>
        /// <returns>True if connection string was found, false otherwise</returns>
        public static bool TryGetDefaultConnectionString(this IConfiguration configuration, out string connectionString)
        {
            connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            return !string.IsNullOrEmpty(connectionString);
        }

        /// <summary>
        /// Tries to get the encryption key without throwing an exception
        /// </summary>
        /// <param name="configuration">The configuration instance</param>
        /// <param name="encryptionKey">The encryption key if found</param>
        /// <returns>True if encryption key was found, false otherwise</returns>
        public static bool TryGetEncryptionKey(this IConfiguration configuration, out string encryptionKey)
        {
            encryptionKey = configuration["Security:EncryptionKey"] ?? string.Empty;
            return !string.IsNullOrEmpty(encryptionKey);
        }

        /// <summary>
        /// Tries to get the blind index key without throwing an exception
        /// </summary>
        /// <param name="configuration">The configuration instance</param>
        /// <param name="blindIndexKey">The blind index key if found</param>
        /// <returns>True if blind index key was found, false otherwise</returns>
        public static bool TryGetBlindIndexKey(this IConfiguration configuration, out string blindIndexKey)
        {
            blindIndexKey = configuration["Security:BlindIndexKey"] ?? string.Empty;
            return !string.IsNullOrEmpty(blindIndexKey);
        }
    }
}

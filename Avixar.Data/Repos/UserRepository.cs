using Avixar.Entity;
using Avixar.Entity.Entities;
using Avixar.Entity.Models;
using Avixar.Infrastructure;
using Avixar.Infrastructure.Extensions;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace Avixar.Data
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connString;
        private readonly string _encKey;
        private readonly string _blindKey;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(IConfiguration config, ILogger<UserRepository> logger)
        {
            _connString = config.GetDefaultConnectionString();
            _encKey = config.GetEncryptionKey();
            _blindKey = config.GetBlindIndexKey();
            _logger = logger;
        }

        public async Task<Guid> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl)
        {
            try
            {
                _logger.LogInformation("Social login attempt - Provider: {Provider}, Email: {Email}", provider, email);
                
                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    using (var cmd = new NpgsqlCommand("CALL sp_SocialLogin(@prov, @sub, @email, @name, @pic, @uid)", conn))
                    {
                        cmd.Parameters.AddWithValue("prov", provider.ToUpper());
                        cmd.Parameters.AddWithValue("sub", subjectId);
                        cmd.Parameters.AddWithValue("email", email ?? "");
                        cmd.Parameters.AddWithValue("name", displayName ?? "User");
                        cmd.Parameters.AddWithValue("pic", (object?)pictureUrl ?? DBNull.Value);

                        var outParam = new NpgsqlParameter("uid", NpgsqlDbType.Uuid)
                        {
                            Direction = ParameterDirection.InputOutput,
                            Value = DBNull.Value
                        };
                        cmd.Parameters.Add(outParam);

                        await cmd.ExecuteNonQueryAsync();

                        if (outParam.Value == DBNull.Value)
                        {
                            _logger.LogError("Social login failed - sp_SocialLogin returned null");
                            throw new Exception("Failed to login/register user via sp_SocialLogin.");
                        }

                        var userId = (Guid)outParam.Value;
                        _logger.LogInformation("Social login successful - Provider: {Provider}, UserId: {UserId}", provider, userId);
                        return userId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during social login - Provider: {Provider}, Email: {Email}", provider, email);
                throw;
            }
        }

        public async Task<UserCredentials?> LoginLocalAsync(string email)
        {
            try
            {
                _logger.LogInformation("Local login attempt for email: {Email}", email);
                
                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    using (var cmd = new NpgsqlCommand(SqlQueries.LoginLocal, conn))
                    {
                        cmd.Parameters.AddWithValue("email", email);
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var userId = reader.GetGuid(0);
                                _logger.LogInformation("Local login successful - UserId: {UserId}", userId);
                                return new UserCredentials
                                {
                                    UserId = userId,
                                    DisplayName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    PasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Email = reader.IsDBNull(3) ? "" : reader.GetString(3)
                                };
                            }
                        }
                    }

                    _logger.LogWarning("Local login failed - User not found for email: {Email}", email);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local login for email: {Email}", email);
                throw;
            }
        }

        public async Task<Guid> RegisterLocalAsync(string email, string password, string displayName)
        {
            try
            {
                _logger.LogInformation("Registration attempt for email: {Email}", email);
                
                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();

                    using (var keyCmd = new NpgsqlCommand($"SET app.enc_key = '{_encKey}'; SET app.blind_key = '{_blindKey}';", conn))
                    {
                        await keyCmd.ExecuteNonQueryAsync();
                    }

                    string passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

                    using (var cmd = new NpgsqlCommand("CALL sp_CreateUser(@name, @email, @mobile, @pass, @newId)", conn))
                    {
                        cmd.Parameters.AddWithValue("name", displayName);
                        cmd.Parameters.AddWithValue("email", email);
                        cmd.Parameters.AddWithValue("mobile", DBNull.Value);
                        cmd.Parameters.AddWithValue("pass", passwordHash);

                        var outParam = new NpgsqlParameter("newId", NpgsqlDbType.Uuid)
                        {
                            Direction = ParameterDirection.InputOutput,
                            Value = DBNull.Value
                        };
                        cmd.Parameters.Add(outParam);

                        try 
                        {
                            await cmd.ExecuteNonQueryAsync();
                            var userId = (Guid)outParam.Value;
                            _logger.LogInformation("Registration successful - UserId: {UserId}", userId);
                            return userId;
                        }
                        catch (PostgresException ex) when (ex.SqlState == "23505")
                        {
                            _logger.LogWarning("Registration failed - User already exists: {Email}", email);
                            throw new Exception("User with this email already exists.");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.Message != "User with this email already exists.")
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", email);
                throw;
            }
        }

        public async Task<ApplicationUser?> GetUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting user: {UserId}", userId);
                
                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    using (var cmd = new NpgsqlCommand(SqlQueries.GetUser, conn))
                    {
                        cmd.Parameters.AddWithValue("uid", userId);
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var user = new ApplicationUser
                                {
                                    Id = reader.GetGuid(0).ToString(),
                                    DisplayName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    ProfilePictureUrl = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    FirstName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    LastName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    Email = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    UserName = reader.IsDBNull(5) ? "" : reader.GetString(5)
                                };
                                
                                _logger.LogInformation("User found: {UserId}", userId);
                                return user;
                            }
                        }
                    }
                }
                
                _logger.LogWarning("User not found: {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateUserAsync(ApplicationUser user)
        {
            try
            {
                _logger.LogInformation("Updating user: {UserId}", user.Id);
                
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(SqlQueries.UpdateUser, conn);
                cmd.Parameters.AddWithValue("Id", Guid.Parse(user.Id));
                cmd.Parameters.AddWithValue("FirstName", (object?)user.FirstName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("LastName", (object?)user.LastName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("DisplayName", (object?)user.DisplayName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ProfilePictureUrl", (object?)user.ProfilePictureUrl ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;
                
                if (success)
                    _logger.LogInformation("Successfully updated user: {UserId}", user.Id);
                else
                    _logger.LogWarning("Failed to update user: {UserId}", user.Id);
                    
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<List<UserAddress>> GetUserAddressesAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting addresses for user: {UserId}", userId);
                
                var addresses = new List<UserAddress>();
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(SqlQueries.GetUserAddresses, conn);
                cmd.Parameters.AddWithValue("UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    addresses.Add(new UserAddress
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("id")),
                        UserId = reader.GetGuid(reader.GetOrdinal("user_id")),
                        Label = reader.IsDBNull(reader.GetOrdinal("label")) ? null : reader.GetString(reader.GetOrdinal("label")),
                        AddressLine1 = reader.GetString(reader.GetOrdinal("address_line_1")),
                        AddressLine2 = reader.IsDBNull(reader.GetOrdinal("address_line_2")) ? null : reader.GetString(reader.GetOrdinal("address_line_2")),
                        City = reader.GetString(reader.GetOrdinal("city")),
                        PostalCode = reader.GetString(reader.GetOrdinal("postal_code")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                    });
                }
                
                _logger.LogInformation("Retrieved {Count} addresses for user: {UserId}", addresses.Count, userId);
                return addresses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting addresses for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AddUserAddressAsync(UserAddress address)
        {
            try
            {
                _logger.LogInformation("Adding address for user: {UserId}", address.UserId);
                
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(SqlQueries.AddUserAddress, conn);
                cmd.Parameters.AddWithValue("Id", address.Id);
                cmd.Parameters.AddWithValue("UserId", address.UserId);
                cmd.Parameters.AddWithValue("Label", (object?)address.Label ?? DBNull.Value);
                cmd.Parameters.AddWithValue("AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("City", address.City);
                cmd.Parameters.AddWithValue("PostalCode", address.PostalCode);
                cmd.Parameters.AddWithValue("CreatedAt", address.CreatedAt);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;
                
                if (success)
                    _logger.LogInformation("Successfully added address {AddressId} for user: {UserId}", address.Id, address.UserId);
                else
                    _logger.LogWarning("Failed to add address for user: {UserId}", address.UserId);
                    
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding address for user: {UserId}", address.UserId);
                throw;
            }
        }

        public async Task<bool> UpdateUserAddressAsync(UserAddress address)
        {
            try
            {
                _logger.LogInformation("Updating address {AddressId} for user: {UserId}", address.Id, address.UserId);
                
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(SqlQueries.UpdateUserAddress, conn);
                cmd.Parameters.AddWithValue("Id", address.Id);
                cmd.Parameters.AddWithValue("UserId", address.UserId);
                cmd.Parameters.AddWithValue("Label", (object?)address.Label ?? DBNull.Value);
                cmd.Parameters.AddWithValue("AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("City", address.City);
                cmd.Parameters.AddWithValue("PostalCode", address.PostalCode);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;
                
                if (success)
                    _logger.LogInformation("Successfully updated address {AddressId}", address.Id);
                else
                    _logger.LogWarning("Failed to update address {AddressId}", address.Id);
                    
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating address {AddressId}", address.Id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAddressAsync(Guid addressId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting address {AddressId} for user: {UserId}", addressId, userId);
                
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(SqlQueries.DeleteUserAddress, conn);
                cmd.Parameters.AddWithValue("Id", addressId);
                cmd.Parameters.AddWithValue("UserId", userId);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;
                
                if (success)
                    _logger.LogInformation("Successfully deleted address {AddressId}", addressId);
                else
                    _logger.LogWarning("Failed to delete address {AddressId}", addressId);
                    
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address {AddressId}", addressId);
                throw;
            }
        }

        private async Task SetDBEncryptionKeyVariables(NpgsqlConnection conn)
        {
            try
            {
                using (var keyCmd = new NpgsqlCommand())
                {
                    keyCmd.Connection = conn;
                    keyCmd.CommandText = DataUtility.GetEncryptionKeys(_encKey, _blindKey);
                    await keyCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to set encryption key variables.", ex);
            }
        }
    }
}

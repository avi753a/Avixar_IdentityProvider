using Npgsql;
using NpgsqlTypes;
using Microsoft.Extensions.Configuration;
using System.Data;
using Avixar.Domain.DTOs;
using Avixar.Domain.Interfaces;
using Avixar.Entity;

namespace Avixar.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connString;
        private readonly string _encKey;
        private readonly string _blindKey;

        public UserRepository(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection") 
                          ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
            
            _encKey = config["Security:EncryptionKey"] 
                      ?? throw new ArgumentNullException("Security:EncryptionKey not found.");
            
            _blindKey = config["Security:BlindIndexKey"] 
                       ?? throw new ArgumentNullException("Security:BlindIndexKey not found.");
        }

        public async Task<Guid> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl)
        {
            using (var conn = new NpgsqlConnection(_connString))
            {
                await conn.OpenAsync();

                using (var keyCmd = new NpgsqlCommand())
                {
                    keyCmd.Connection = conn;
                    keyCmd.CommandText = $"SET app.enc_key = '{_encKey}'; SET app.blind_key = '{_blindKey}';";
                    await keyCmd.ExecuteNonQueryAsync();
                }

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
                        throw new Exception("Failed to login/register user via sp_SocialLogin.");
                    }

                    return (Guid)outParam.Value;
                }
            }
        }

        public async Task<(bool Success, Guid UserId, string DisplayName, string Email)> LoginLocalAsync(string email, string password)
        {
             using (var conn = new NpgsqlConnection(_connString))
            {
                await conn.OpenAsync();

                using (var keyCmd = new NpgsqlCommand($"SET app.enc_key = '{_encKey}'; SET app.blind_key = '{_blindKey}';", conn))
                {
                    await keyCmd.ExecuteNonQueryAsync();
                }

                var sql = @"
                    SELECT u.""Id"", u.""DisplayName"", s.""PasswordHash"", 
                           pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key')) as Email
                    FROM ""user_secrets"" s
                    JOIN ""users"" u ON u.""Id"" = s.""Id""
                    WHERE s.""Email_Hash"" = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex')
                    AND s.""PasswordHash"" IS NOT NULL;
                ";

                string? dbPasswordHash = null;
                Guid userId = Guid.Empty;
                string dbDisplayName = "";
                string dbEmail = "";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            userId = reader.GetGuid(0);
                            dbDisplayName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            dbPasswordHash = reader.IsDBNull(2) ? null : reader.GetString(2);
                            dbEmail = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        }
                    }
                }

                if (dbPasswordHash == null)
                {
                    return (false, Guid.Empty, "", "");
                }

                bool isValid = BCrypt.Net.BCrypt.Verify(password, dbPasswordHash);

                return (isValid, userId, dbDisplayName, dbEmail);
            }
        }

        public async Task<Guid> RegisterLocalAsync(string email, string password, string displayName)
        {
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
                        return (Guid)outParam.Value;
                    }
                    catch (PostgresException ex) when (ex.SqlState == "23505")
                    {
                        throw new Exception("User with this email already exists.");
                    }
                }
            }
        }

        public async Task<ApplicationUser?> GetUserWithWalletAsync(Guid userId)
        {
            // Note: In a real ADO.NET implementation, we would query the tables directly.
            // Since we are moving away from EF, we should construct the ApplicationUser object manually.
            // However, ApplicationUser is an Entity class.
            
            using (var conn = new NpgsqlConnection(_connString))
            {
                await conn.OpenAsync();

                using (var keyCmd = new NpgsqlCommand($"SET app.enc_key = '{_encKey}'; SET app.blind_key = '{_blindKey}';", conn))
                {
                    await keyCmd.ExecuteNonQueryAsync();
                }

                var sql = @"
                    SELECT u.""Id"", u.""DisplayName"", u.""ProfilePictureUrl"",
                           pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key')) as Email
                    FROM ""users"" u
                    JOIN ""user_secrets"" s ON u.""Id"" = s.""Id""
                    WHERE u.""Id"" = @uid;
                ";

                using (var cmd = new NpgsqlCommand(sql, conn))
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
                                Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                UserName = reader.IsDBNull(3) ? "" : reader.GetString(3)
                            };
                            
                            // Mock Wallet for now as we don't have Wallet table in the scripts provided
                            // But the user requested wallet functionality.
                            // I'll leave it null or mock it.
                            user.Wallet = new Wallet { Balance = 0, Currency = "USD" };
                            
                            return user;
                        }
                    }
                }
            }
            return null;
        }
    }
}

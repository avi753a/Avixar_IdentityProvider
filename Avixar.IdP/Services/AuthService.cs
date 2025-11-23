using Npgsql;
using NpgsqlTypes;
using BCrypt.Net;

namespace Avixar.IdP.Services
{
    public class AuthService
    {
        private readonly string _connectionString;
        private readonly string _encKey;
        private readonly string _blindKey;

        public AuthService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _encKey = config["Security:EncryptionKey"];
            _blindKey = config["Security:BlindIndexKey"];
        }

        public async Task<Guid> RegisterWithEmailAsync(string name, string email, string plainPassword, string mobile = null)
        {
            // 1. Hash the Password in C# (CPU Intensive work belongs in App, not DB)
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 11);

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 2. Set Keys in Session
                using (var keyCmd = new NpgsqlCommand($"SET app.enc_key = '{_encKey}'; SET app.blind_key = '{_blindKey}';", conn))
                {
                    await keyCmd.ExecuteNonQueryAsync();
                }

                // 3. Call the Procedure
                using (var cmd = new NpgsqlCommand("CALL sp_CreateUser(@name, @email, @mobile, @pass, @uid)", conn))
                {
                    cmd.Parameters.AddWithValue("name", name);
                    cmd.Parameters.AddWithValue("email", email);
                    cmd.Parameters.AddWithValue("mobile", (object)mobile ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("pass", passwordHash);

                    var outParam = new NpgsqlParameter("uid", NpgsqlDbType.Uuid)
                    {
                        Direction = System.Data.ParameterDirection.InputOutput,
                        Value = DBNull.Value
                    };
                    cmd.Parameters.Add(outParam);

                    try 
                    {
                        await cmd.ExecuteNonQueryAsync();
                        return (Guid)outParam.Value;
                    }
                    catch (PostgresException ex) when (ex.MessageText.Contains("USER_EXISTS") || ex.SqlState == "23505")
                    {
                        throw new InvalidOperationException("This email is already registered.");
                    }
                }
            }
        }

        public async Task<Guid> SocialLoginAsync(string provider, string subjectId, string email, string displayName, string profilePicUrl)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Set Keys
                using (var keyCmd = new NpgsqlCommand($"SET app.enc_key = '{_encKey}'; SET app.blind_key = '{_blindKey}';", conn))
                {
                    await keyCmd.ExecuteNonQueryAsync();
                }

                // We cast the provider string to the enum type in SQL
                using (var cmd = new NpgsqlCommand("CALL sp_SocialLogin(@provider::auth_provider, @subId, @email, @name, @pic, @uid)", conn))
                {
                    cmd.Parameters.AddWithValue("provider", provider.ToUpper()); // Ensure uppercase for ENUM
                    cmd.Parameters.AddWithValue("subId", subjectId);
                    cmd.Parameters.AddWithValue("email", (object)email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("name", (object)displayName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("pic", (object)profilePicUrl ?? DBNull.Value);

                    var outParam = new NpgsqlParameter("uid", NpgsqlDbType.Uuid)
                    {
                        Direction = System.Data.ParameterDirection.InputOutput,
                        Value = DBNull.Value
                    };
                    cmd.Parameters.Add(outParam);

                    await cmd.ExecuteNonQueryAsync();
                    return (Guid)outParam.Value;
                }
            }
        }

        public async Task<bool> ValidateUserAsync(string email, string password)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var keyCmd = new NpgsqlCommand($"SET app.blind_key = '{_blindKey}';", conn))
                {
                    await keyCmd.ExecuteNonQueryAsync();
                }

                // Query User_Secrets to get the hash
                // We calculate the email hash in SQL using the blind key
                var sql = @"
                    SELECT ""PasswordHash"" 
                    FROM ""User_Secrets"" 
                    WHERE ""Email_Hash"" = encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex')";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);
                    var dbHash = await cmd.ExecuteScalarAsync() as string;

                    if (dbHash != null)
                    {
                        return BCrypt.Net.BCrypt.Verify(password, dbHash);
                    }
                }
            }
            return false;
        }
    }
}


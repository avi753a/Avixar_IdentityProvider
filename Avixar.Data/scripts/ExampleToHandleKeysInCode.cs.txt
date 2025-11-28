using Npgsql;
using Microsoft.Extensions.Configuration;

public class UserRepository
{
    private readonly string _connString;
    private readonly string _encKey;
    private readonly string _blindKey;

    public UserRepository(IConfiguration config)
    {
        _connString = config.GetConnectionString("DefaultConnection");
        
        // Load keys from appsettings.json or Environment Variables (Azure KeyVault)
        _encKey = config["Security:EncryptionKey"]; 
        _blindKey = config["Security:BlindIndexKey"];
    }

    public async Task<Guid> CreateUserAsync(UserDto user)
    {
        using (var conn = new NpgsqlConnection(_connString))
        {
            await conn.OpenAsync();

            // ---------------------------------------------------------
            // STEP 1: Load Keys into Session (Zero IO Overhead)
            // ---------------------------------------------------------
            // We use string interpolation carefully here because keys are trusted config values, 
            // NOT user input (so SQL injection isn't a risk from the config).
            using (var keyCmd = new NpgsqlCommand())
            {
                keyCmd.Connection = conn;
                keyCmd.CommandText = $"SET app.enc_key = '{_encKey}'; " +
                                     $"SET app.blind_key = '{_blindKey}';";
                await keyCmd.ExecuteNonQueryAsync();
            }

            // ---------------------------------------------------------
            // STEP 2: Run the Actual Procedure
            // ---------------------------------------------------------
            // The Proc doesn't take keys as params. It reads them from the Session we just set.
            using (var cmd = new NpgsqlCommand("CALL sp_CreateUser(@name, @email, @mobile, @pass, @newId)", conn))
            {
                cmd.Parameters.AddWithValue("name", user.DisplayName);
                // Pass Plain Text - The DB will use the Session Key to Encrypt it
                cmd.Parameters.AddWithValue("email", user.Email); 
                cmd.Parameters.AddWithValue("mobile", user.Mobile);
                cmd.Parameters.AddWithValue("pass", user.PasswordHash);

                var outParam = new NpgsqlParameter("newId", NpgsqlDbType.Uuid)
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
}
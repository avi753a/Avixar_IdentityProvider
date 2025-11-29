using Npgsql;
using System;
using System.Threading.Tasks;

namespace Avixar.DbPatcher;

public class SampleDataSeeder
{
    public static async Task Run()
    {
        Console.WriteLine("Avixar Identity Provider");
        Console.WriteLine("Sample Data Seeder");
        Console.WriteLine("========================================");
        Console.WriteLine();

        var connectionString = "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123";

        try
        {
            Console.WriteLine("[1/4] Connecting to database...");
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            Console.WriteLine("✓ Connected successfully");
            Console.WriteLine();

            Console.WriteLine("[2/4] Recreating clients table...");
            var createTableSql = @"
                DROP TABLE IF EXISTS clients CASCADE;
                CREATE TABLE clients (
                    client_id TEXT PRIMARY KEY,
                    client_name TEXT NOT NULL,
                    client_secret TEXT,
                    allowed_redirect_uris TEXT[],
                    allowed_logout_uris TEXT[],
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT NOW()
                );";
            using (var cmd = new NpgsqlCommand(createTableSql, conn))
            {
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("✓ Table recreated successfully");
            }
            Console.WriteLine();

            Console.WriteLine("[3/4] Inserting sample OAuth clients...");
            
            var insertSql = @"
                INSERT INTO clients (
                    client_id, 
                    client_name, 
                    client_secret,
                    allowed_redirect_uris, 
                    allowed_logout_uris,
                    is_active
                )
                VALUES (
                    @client_id, 
                    @client_name, 
                    @client_secret,
                    @allowed_redirect_uris, 
                    @allowed_logout_uris,
                    @is_active
                )";

            // Insert test client 1
            await using (var cmd = new NpgsqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("client_id", "test_client_123");
                cmd.Parameters.AddWithValue("client_name", "Test Chat Application");
                cmd.Parameters.AddWithValue("client_secret", "test_secret_456");
                cmd.Parameters.AddWithValue("allowed_redirect_uris", new[] {
                    "http://localhost:3000/callback",
                    "http://localhost:3000/auth/callback",
                    "https://oauth.pstmn.io/v1/callback"
                });
                cmd.Parameters.AddWithValue("allowed_logout_uris", new[] {
                    "http://localhost:3000/",
                    "http://localhost:3000/logout"
                });
                cmd.Parameters.AddWithValue("is_active", true);

                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("  ✓ Inserted: test_client_123");
            }

            // Insert test client 2
            await using (var cmd = new NpgsqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("client_id", "mobile_app_001");
                cmd.Parameters.AddWithValue("client_name", "Mobile App Client");
                cmd.Parameters.AddWithValue("client_secret", "mobile_secret_789");
                cmd.Parameters.AddWithValue("allowed_redirect_uris", new[] {
                    "myapp://callback",
                    "http://localhost:8080/callback"
                });
                cmd.Parameters.AddWithValue("allowed_logout_uris", new[] {
                    "myapp://logout",
                    "http://localhost:8080/"
                });
                cmd.Parameters.AddWithValue("is_active", true);

                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("  ✓ Inserted: mobile_app_001");
            }

            Console.WriteLine();
            Console.WriteLine("[4/4] Verifying data...");

            var verifySql = "SELECT client_id, client_name, is_active FROM clients ORDER BY created_at DESC LIMIT 2";
            await using (var cmd = new NpgsqlCommand(verifySql, conn))
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                Console.WriteLine();
                Console.WriteLine("  Client ID          | Client Name              | Active");
                Console.WriteLine("  -------------------|--------------------------|-------");
                
                while (await reader.ReadAsync())
                {
                    var clientId = reader.GetString(0);
                    var clientName = reader.GetString(1);
                    var isActive = reader.GetBoolean(2);
                    Console.WriteLine($"  {clientId,-18} | {clientName,-24} | {(isActive ? "Yes" : "No")}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("✓ Sample data inserted successfully!");
            Console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("❌ ERROR: " + ex.Message);
            Environment.Exit(1);
        }
    }
}

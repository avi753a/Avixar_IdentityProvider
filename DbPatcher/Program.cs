using Npgsql;
using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string connString = "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123";
        string scriptPath = Path.Combine("..", "Avixar.Data", "scripts", "FixDatabase_VerticalPartitioning.sql");

        try
        {
            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"Script not found at: {Path.GetFullPath(scriptPath)}");
                return;
            }

            string script = File.ReadAllText(scriptPath);
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(script, conn))
                {
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("Successfully executed FixDatabase_VerticalPartitioning.sql - Schema rebuilt.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}

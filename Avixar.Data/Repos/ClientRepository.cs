using Avixar.Entity.Entities;
using Avixar.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Threading.Tasks;

namespace Avixar.Data
{
    public class ClientRepository : IClientRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientRepository> _logger;

        public ClientRepository(IConfiguration config, ILogger<ClientRepository> logger)
        {
            _connectionString = config.GetDefaultConnectionString();
            _logger = logger;
        }

        public async Task<Client?> GetClientAsync(string clientId)
        {
            try
            {
                _logger.LogInformation("Getting client: {ClientId}", clientId);
                
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = SqlQueries.GetClient;
                cmd.Parameters.AddWithValue("Id", clientId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var client = new Client
                    {
                        ClientId = reader.GetString(reader.GetOrdinal("client_id")),
                        ClientName = reader.GetString(reader.GetOrdinal("client_name")),
                        ClientSecret = reader.GetString(reader.GetOrdinal("client_secret")),
                        AllowedRedirectUris = reader.IsDBNull(reader.GetOrdinal("allowed_redirect_uris"))
                            ? Array.Empty<string>()
                            : reader.GetFieldValue<string[]>(reader.GetOrdinal("allowed_redirect_uris")),
                        AllowedLogoutUris = reader.IsDBNull(reader.GetOrdinal("allowed_logout_uris"))
                            ? Array.Empty<string>()
                            : reader.GetFieldValue<string[]>(reader.GetOrdinal("allowed_logout_uris"))
                    };
                    
                    _logger.LogInformation("Client found: {ClientId}", clientId);
                    return client;
                }
                
                _logger.LogWarning("Client not found: {ClientId}", clientId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client: {ClientId}", clientId);
                throw;
            }
        }

        public async Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret)
        {
            try
            {
                _logger.LogInformation("Validating client secret for: {ClientId}", clientId);
                
                var client = await GetClientAsync(clientId);
                var isValid = client != null && client.ClientSecret == clientSecret;
                
                if (isValid)
                    _logger.LogInformation("Client secret valid for: {ClientId}", clientId);
                else
                    _logger.LogWarning("Client secret invalid for: {ClientId}", clientId);
                    
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating client secret for: {ClientId}", clientId);
                throw;
            }
        }
    }
}

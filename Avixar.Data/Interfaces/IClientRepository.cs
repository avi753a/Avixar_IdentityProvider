using Avixar.Entity.Entities;

namespace Avixar.Data
{
    public interface IClientRepository
    {
        Task<Client?> GetClientAsync(string clientId);
        Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret);
    }
}

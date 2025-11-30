namespace Avixar.Domain
{
    public interface ICacheService
    {
        Task SetAsync<T>(string key, T value, TimeSpan expiry);
        Task<T?> GetAsync<T>(string key);
        Task RemoveAsync(string key);
    }
}

using Avixar.Entity;
using Avixar.Entity.Models;
using Avixar.Entity.Entities;

namespace Avixar.Domain
{
    public interface IVerificationService
    {
        Task<BaseReturn<bool>> SendVerificationEmailAsync(Guid userId, string email, string baseUrl);
        Task<BaseReturn<bool>> VerifyEmailWithTokenAsync(string token);
        Task<BaseReturn<string>> SendOtpAsync(Guid userId, string email, OtpPurpose purpose, int? expirySeconds = null);
        Task<BaseReturn<bool>> ValidateOtpAsync(Guid userId, string code, OtpPurpose purpose);
        Task<BaseReturn<bool>> HasValidOtpAsync(Guid userId, OtpPurpose purpose);
        Task<BaseReturn<int>> GetRemainingAttemptsAsync(Guid userId, OtpPurpose purpose);
    }
}

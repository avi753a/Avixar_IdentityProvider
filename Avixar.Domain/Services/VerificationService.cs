using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity.Models;
using Avixar.Entity.Entities;
using Avixar.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    public class VerificationService : IVerificationService
    {
        private readonly VerificationTokenService _tokenService;
        private readonly OtpService _otpService;
        private readonly EmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<VerificationService> _logger;

        public VerificationService(
            VerificationTokenService tokenService,
            OtpService otpService,
            EmailService emailService,
            IUserRepository userRepository,
            ILogger<VerificationService> logger)
        {
            _tokenService = tokenService;
            _otpService = otpService;
            _emailService = emailService;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<BaseReturn<bool>> SendVerificationEmailAsync(Guid userId, string email, string baseUrl)
        {
            try
            {
                _logger.LogInformation("Sending verification email to {Email}", email);

                // Generate verification token
                var token = await _tokenService.GenerateVerificationTokenAsync(userId, email);

                // Create verification link
                // Ensure baseUrl ends with / if needed, or handle path combination carefully
                // Assuming baseUrl is like "https://host/api/verification/verify-email" or just the host
                // The controller logic was: $"{Request.Scheme}://{Request.Host}/api/verification/verify-email?token={token}"
                // We will pass the full base URL for the endpoint from the controller
                
                var verificationLink = $"{baseUrl}?token={token}";

                // Send email
                await _emailService.SendVerificationEmailAsync(email, verificationLink);
                
                return BaseReturn<bool>.Success(true, "Verification email sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email to {Email}", email);
                return BaseReturn<bool>.Failure($"Failed to send verification email: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> VerifyEmailWithTokenAsync(string token)
        {
            try
            {
                _logger.LogInformation("Verifying email with token");

                var (success, userId, email) = await _tokenService.ValidateTokenAsync(token);

                if (!success)
                {
                    _logger.LogWarning("Invalid or expired verification token");
                    return BaseReturn<bool>.Failure("Invalid or expired token");
                }

                // Mark email as verified in user settings
                await _userRepository.MarkEmailAsVerifiedAsync(userId);

                _logger.LogInformation("Email verified successfully for user {UserId}", userId);
                return BaseReturn<bool>.Success(true, "Email verified successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email with token");
                return BaseReturn<bool>.Failure($"Failed to verify email: {ex.Message}");
            }
        }

        public async Task<BaseReturn<string>> SendOtpAsync(Guid userId, string email, OtpPurpose purpose, int? expirySeconds = null)
        {
            try
            {
                _logger.LogInformation("Sending OTP for {Purpose} to {Email}", purpose, email);

                // Generate OTP with custom expiry if provided
                var otp = await _otpService.GenerateOtpAsync(userId, email, purpose, expirySeconds);

                // Send OTP email
                var purposeText = purpose == OtpPurpose.TwoFactorAuth ? "Two-Factor Authentication" : "Email Update";
                await _emailService.SendOtpEmailAsync(email, otp, purposeText);
                
                return BaseReturn<string>.Success(otp, "OTP sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP for {Purpose} to {Email}", purpose, email);
                return BaseReturn<string>.Failure($"Failed to send OTP: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> ValidateOtpAsync(Guid userId, string code, OtpPurpose purpose)
        {
            try
            {
                _logger.LogInformation("Validating OTP for user {UserId}, purpose {Purpose}", userId, purpose);

                // Validate OTP
                var isValid = await _otpService.ValidateOtpAsync(userId, code, purpose);

                if (isValid)
                {
                    _logger.LogInformation("OTP validated successfully for user {UserId}", userId);
                    return BaseReturn<bool>.Success(true, "OTP validated successfully");
                }

                return BaseReturn<bool>.Failure("Invalid OTP code");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating OTP for user {UserId}", userId);
                return BaseReturn<bool>.Failure($"Failed to validate OTP: {ex.Message}");
            }
        }

        public async Task<BaseReturn<int>> GetRemainingAttemptsAsync(Guid userId, OtpPurpose purpose)
        {
            try
            {
                var attempts = await _otpService.GetRemainingAttemptsAsync(userId, purpose);
                return BaseReturn<int>.Success(attempts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting remaining attempts for user {UserId}", userId);
                return BaseReturn<int>.Failure($"Failed to get remaining attempts: {ex.Message}");
            }
        }

        public async Task<BaseReturn<bool>> HasValidOtpAsync(Guid userId, OtpPurpose purpose)
        {
            try
            {
                var hasOtp = await _otpService.HasValidOtpAsync(userId, purpose);
                return BaseReturn<bool>.Success(hasOtp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OTP validity for user {UserId}", userId);
                return BaseReturn<bool>.Failure($"Failed to check OTP validity: {ex.Message}");
            }
        }
    }
}

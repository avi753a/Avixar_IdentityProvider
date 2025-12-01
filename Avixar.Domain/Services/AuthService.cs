using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity.Models;
using Avixar.Entity.Entities;
using Avixar.Infrastructure.Services;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthService> _logger;
        private readonly OtpService _otpService;
        private readonly EmailService _emailService;
        private readonly VerificationTokenService _verificationTokenService;

        public AuthService(
            IUserRepository userRepository, 
            TokenService tokenService, 
            ILogger<AuthService> logger,
            OtpService otpService,
            EmailService emailService,
            VerificationTokenService verificationTokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _logger = logger;
            _otpService = otpService;
            _emailService = emailService;
            _verificationTokenService = verificationTokenService;
        }

        public async Task<BaseReturn<LoginResult>> RegisterAsync(RegisterDto dto)
        {
            try
            {
                _logger.LogInformation("Registration attempt for email: {Email}", dto.Email);
                
                // Validate input
                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                {
                    _logger.LogWarning("Registration failed: Email or password missing");
                    return BaseReturn<LoginResult>.Failure("Email and password are required");
                }

                if (dto.Password.Length < 6)
                {
                    _logger.LogWarning("Registration failed: Password too short for {Email}", dto.Email);
                    return BaseReturn<LoginResult>.Failure("Password must be at least 6 characters long");
                }

                // Register via Repository
                var userId = await _userRepository.RegisterLocalAsync(dto.Email, dto.Password, dto.DisplayName);

                // Generate JWT token
                var token = _tokenService.GenerateJwtToken(userId.ToString(), dto.Email, dto.DisplayName);

                var result = new LoginResult
                {
                    UserId = userId,
                    Email = dto.Email,
                    DisplayName = dto.DisplayName,
                    Token = token
                };

                _logger.LogInformation("User registered successfully: {UserId}", userId);
                return BaseReturn<LoginResult>.Success(result, "Registration successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for email: {Email}", dto.Email);
                return BaseReturn<LoginResult>.Failure($"Registration failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<LoginResult>> LoginAsync(LoginDto dto)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", dto.Email);
                
                // Validate input
                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                {
                    _logger.LogWarning("Login failed: Email or password missing");
                    return BaseReturn<LoginResult>.Failure("Email and password are required");
                }

                // Get User Credentials from Repository
                var userCreds = await _userRepository.LoginLocalAsync(dto.Email);

                if (userCreds == null)
                {
                    _logger.LogWarning("Login failed: User not found for email: {Email}", dto.Email);
                    return BaseReturn<LoginResult>.Failure("Invalid email or password");
                }

                // Validate Password
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, userCreds.PasswordHash);

                if (!isPasswordValid)
                {
                    _logger.LogWarning("Login failed: Invalid password for email: {Email}", dto.Email);
                    return BaseReturn<LoginResult>.Failure("Invalid email or password");
                }

                // Generate JWT token
                var token = _tokenService.GenerateJwtToken(
                    userCreds.UserId.ToString(), 
                    userCreds.Email, 
                    userCreds.DisplayName
                );

                var result = new LoginResult
                {
                    UserId = userCreds.UserId,
                    Email = userCreds.Email,
                    DisplayName = userCreds.DisplayName,
                    Token = token,
                    ProfilePictureUrl = userCreds.ProfilePictureUrl
                };

                _logger.LogInformation("User logged in successfully: {UserId}", userCreds.UserId);
                return BaseReturn<LoginResult>.Success(result, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for email: {Email}", dto.Email);
                return BaseReturn<LoginResult>.Failure($"Login failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<LoginResult>> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl)
        {
            try
            {
                _logger.LogInformation("Social login attempt - Provider: {Provider}, Email: {Email}", provider, email);
                
                var userId = await _userRepository.LoginWithSocialAsync(provider, subjectId, email, displayName, pictureUrl);
                
                // Generate JWT token
                var token = _tokenService.GenerateJwtToken(userId.ToString(), email, displayName);

                var result = new LoginResult
                {
                    UserId = userId,
                    Email = email,
                    DisplayName = displayName,
                    Token = token
                };

                _logger.LogInformation("Social login successful - Provider: {Provider}, UserId: {UserId}", provider, userId);
                return BaseReturn<LoginResult>.Success(result, "Social login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Social login failed - Provider: {Provider}, Email: {Email}", provider, email);
                return BaseReturn<LoginResult>.Failure($"Social login failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<LoginResult>> RegisterWithVerificationAsync(RegisterDto dto, string verificationBaseUrl)
        {
            try
            {
                // 1. Register User
                var registerResult = await RegisterAsync(dto);
                if (!registerResult.Status) return registerResult;

                var user = registerResult.Data;

                // 2. Auto-verify email on signup (as per original controller logic)
                await _userRepository.MarkEmailAsVerifiedAsync(user.UserId);
                
                // 3. Send Verification Email (for record/welcome)
                var token = await _verificationTokenService.GenerateVerificationTokenAsync(user.UserId, user.Email);
                var verificationLink = $"{verificationBaseUrl}?token={token}";
                await _emailService.SendVerificationEmailAsync(user.Email, verificationLink);

                return registerResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration with verification failed for email: {Email}", dto.Email);
                return BaseReturn<LoginResult>.Failure($"Registration failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<(bool requires2FA, LoginResult? user)>> LoginWithTwoFactorCheckAsync(LoginDto dto)
        {
            try
            {
                // 1. Basic Login
                var loginResult = await LoginAsync(dto);
                if (!loginResult.Status) 
                    return BaseReturn<(bool, LoginResult?)>.Failure(loginResult.Message);

                var user = loginResult.Data;

                // 2. Check 2FA
                var userSettings = await _userRepository.GetUserSettingsAsync(user.UserId);
                if (userSettings != null && userSettings.TwoFactorEnabled)
                {
                    // Send OTP
                    var otp = await _otpService.GenerateOtpAsync(user.UserId, user.Email, OtpPurpose.TwoFactorAuth);
                    await _emailService.SendOtpEmailAsync(user.Email, otp, "Two-Factor Authentication");

                    // Return indicating 2FA is required, but pass user data (without token if we wanted to be strict, but we need userId)
                    // We can return the user object but the controller should know not to sign them in yet
                    return BaseReturn<(bool, LoginResult?)>.Success((true, user), "2FA required");
                }

                // No 2FA required
                return BaseReturn<(bool, LoginResult?)>.Success((false, user), "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login with 2FA check failed for email: {Email}", dto.Email);
                return BaseReturn<(bool, LoginResult?)>.Failure($"Login failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<LoginResult>> VerifyTwoFactorAndLoginAsync(Guid userId, string code)
        {
            try
            {
                var isValid = await _otpService.ValidateOtpAsync(userId, code, OtpPurpose.TwoFactorAuth);
                if (!isValid)
                {
                    return BaseReturn<LoginResult>.Failure("Invalid Code");
                }

                var user = await _userRepository.GetUserAsync(userId);
                if (user == null) return BaseReturn<LoginResult>.Failure("User not found");

                // Generate Token
                var token = _tokenService.GenerateJwtToken(user.Id.ToString(), user.Email, user.DisplayName);

                var result = new LoginResult 
                { 
                    UserId = Guid.Parse(user.Id), 
                    Email = user.Email ?? "", 
                    DisplayName = user.DisplayName ?? "",
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    Token = token
                };
                
                return BaseReturn<LoginResult>.Success(result, "2FA verified successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA verification failed for user: {UserId}", userId);
                return BaseReturn<LoginResult>.Failure($"Verification failed: {ex.Message}");
            }
        }
    }
}

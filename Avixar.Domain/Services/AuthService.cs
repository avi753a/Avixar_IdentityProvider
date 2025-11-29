using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity.Models;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, TokenService tokenService, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _logger = logger;
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
                    Token = token
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
    }
}

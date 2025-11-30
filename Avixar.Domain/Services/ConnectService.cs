using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity.Models;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Avixar.Domain
{
    public class ConnectService : IConnectService
    {
        private readonly IClientRepository _clientRepository;
        private readonly ICacheService _cacheService;
        private readonly TokenService _tokenService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ConnectService> _logger;

        public ConnectService(
            IClientRepository clientRepository,
            ICacheService cacheService,
            TokenService tokenService,
            IUserRepository userRepository,
            ILogger<ConnectService> logger)
        {
            _clientRepository = clientRepository;
            _cacheService = cacheService;
            _tokenService = tokenService;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<BaseReturn<string>> AuthorizeAsync(ExternalLoginRequest request, ClaimsPrincipal user)
        {
            try
            {
                _logger.LogInformation("OAuth Authorize request - ClientId: {ClientId}, RedirectUri: {RedirectUri}", 
                    request.client_id, request.redirect_uri);

                // 1. Validate Client
                var client = await _clientRepository.GetClientAsync(request.client_id);
                if (client == null)
                {
                    _logger.LogWarning("Invalid client ID: {ClientId}", request.client_id);
                    return BaseReturn<string>.Failure("Invalid Client ID");
                }

                // 2. Validate Redirect URI
                if (!client.AllowedRedirectUris.Contains(request.redirect_uri))
                {
                    _logger.LogWarning("Unauthorized redirect URI: {RedirectUri} for client: {ClientId}", 
                        request.redirect_uri, request.client_id);
                    return BaseReturn<string>.Failure("Unauthorized Redirect URI");
                }

                // 3. SSO Check
                if (user == null || !user.Identity!.IsAuthenticated)
                {
                    _logger.LogInformation("User not authenticated for OAuth authorize");
                    return BaseReturn<string>.Failure("NotAuthenticated");
                }

                // 4. Generate Authorization Code
                var code = Guid.NewGuid().ToString("N");

                // 5. Store Code in Redis (TTL: 5 minutes)
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = user.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User identifier missing in claims");
                    return BaseReturn<string>.Failure("User identifier missing");
                }

                var authData = new AuthCodeData
                {
                    UserId = Guid.Parse(userId),
                    Email = email ?? "",
                    ClientId = request.client_id,
                    RedirectUri = request.redirect_uri,
                    Nonce = request.nonce ?? "",
                    CreatedAt = DateTime.UtcNow
                };

                await _cacheService.SetAsync($"auth_code:{code}", authData, TimeSpan.FromMinutes(5));

                // 6. Build Callback URL
                var callbackUrl = $"{request.redirect_uri}?code={code}";
                if (!string.IsNullOrEmpty(request.state))
                    callbackUrl += $"&state={request.state}";

                _logger.LogInformation("Authorization code generated for user: {UserId}, client: {ClientId}", 
                    userId, request.client_id);
                return BaseReturn<string>.Success(callbackUrl, "Authorization successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authorization failed for client: {ClientId}", request.client_id);
                return BaseReturn<string>.Failure($"Authorization failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<TokenResponse>> ExchangeTokenAsync(string clientId, string clientSecret, string code, string redirectUri)
        {
            try
            {
                _logger.LogInformation("Token exchange request - ClientId: {ClientId}", clientId);

                // 1. Validate Client Secret
                var isValidClient = await _clientRepository.ValidateClientSecretAsync(clientId, clientSecret);
                if (!isValidClient)
                {
                    _logger.LogWarning("Invalid client credentials for: {ClientId}", clientId);
                    return BaseReturn<TokenResponse>.Failure("Invalid Client Credentials");
                }

                // 2. Retrieve Code from Redis
                var authData = await _cacheService.GetAsync<AuthCodeData>($"auth_code:{code}");
                if (authData == null)
                {
                    _logger.LogWarning("Invalid or expired authorization code");
                    return BaseReturn<TokenResponse>.Failure("Invalid or expired authorization code");
                }

                // 3. Validate Code Bindings
                if (authData.ClientId != clientId)
                {
                    _logger.LogWarning("Code client mismatch - Expected: {Expected}, Got: {Got}", 
                        authData.ClientId, clientId);
                    return BaseReturn<TokenResponse>.Failure("Invalid code for this client");
                }

                if (authData.RedirectUri != redirectUri)
                {
                    _logger.LogWarning("Redirect URI mismatch for code exchange");
                    return BaseReturn<TokenResponse>.Failure("Invalid redirect_uri binding");
                }

                // 4. Generate JWT
                var user = await _userRepository.GetUserAsync(authData.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for token generation: {UserId}", authData.UserId);
                    return BaseReturn<TokenResponse>.Failure("User not found");
                }

                var accessToken = _tokenService.GenerateJwtToken(user.Id.ToString(), user.Email ?? "", user.DisplayName ?? "");

                // 5. Delete Code (Anti-replay)
                await _cacheService.RemoveAsync($"auth_code:{code}");

                _logger.LogInformation("Token generated successfully for user: {UserId}, client: {ClientId}", 
                    authData.UserId, clientId);
                
                return BaseReturn<TokenResponse>.Success(new TokenResponse
                {
                    access_token = accessToken,
                    expires_in = 3600,
                    id_token = "TODO: Implement ID Token if needed" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token exchange failed for client: {ClientId}", clientId);
                return BaseReturn<TokenResponse>.Failure($"Token exchange failed: {ex.Message}");
            }
        }

        public async Task<BaseReturn<UserProfileDto>> GetUserInfoAsync(string userId)
        {
            try
            {
                _logger.LogInformation("UserInfo request for: {UserId}", userId);

                if (!Guid.TryParse(userId, out var guid))
                {
                    _logger.LogWarning("Invalid user ID format: {UserId}", userId);
                    return BaseReturn<UserProfileDto>.Failure("Invalid User ID");
                }

                var user = await _userRepository.GetUserAsync(guid);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return BaseReturn<UserProfileDto>.Failure("User not found");
                }

                var profile = new UserProfileDto
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    DisplayName = user.DisplayName ?? "",
                    ImageUrl = user.ProfilePictureUrl
                };

                _logger.LogInformation("UserInfo retrieved successfully for: {UserId}", userId);
                return BaseReturn<UserProfileDto>.Success(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user info for: {UserId}", userId);
                return BaseReturn<UserProfileDto>.Failure($"Failed to retrieve user info: {ex.Message}");
            }
        }

        public async Task<bool> ValidateLogoutUriAsync(string clientId, string logoutUri)
        {
            try
            {
                _logger.LogInformation("Validating logout URI for client: {ClientId}", clientId);
                
                var client = await _clientRepository.GetClientAsync(clientId);
                var isValid = client != null && client.AllowedLogoutUris.Contains(logoutUri);
                
                if (isValid)
                    _logger.LogInformation("Logout URI valid for client: {ClientId}", clientId);
                else
                    _logger.LogWarning("Logout URI invalid for client: {ClientId}", clientId);
                    
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating logout URI for client: {ClientId}", clientId);
                return false;
            }
        }
    }
}

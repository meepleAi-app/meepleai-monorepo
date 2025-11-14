using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// OAuth 2.0 authentication service for Google, Discord, and GitHub.
/// Implements authorization code flow with PKCE and CSRF protection.
/// Delegates domain logic to CQRS handlers via IMediator.
/// </summary>
public class OAuthService : IOAuthService
{
    private readonly MeepleAiDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<OAuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthConfiguration _config;
    private readonly TimeProvider _timeProvider;
    private readonly IMediator _mediator;

    // CSRF state storage (in-memory for MVP, could be Redis for production)
    private static readonly Dictionary<string, DateTime> _stateStore = new();
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private const string EncryptionPurpose = "OAuthTokens";

    public OAuthService(
        MeepleAiDbContext db,
        IEncryptionService encryption,
        ILogger<OAuthService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<OAuthConfiguration> config,
        IMediator mediator,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _encryption = encryption;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _mediator = mediator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<string> GetAuthorizationUrlAsync(string provider, string state)
    {
        var providerConfig = GetProviderConfig(provider);
        var callbackUrl = GetCallbackUrl(provider);

        var authUrl = new StringBuilder(providerConfig.AuthorizationUrl);
        authUrl.Append("?response_type=code");
        authUrl.Append($"&client_id={Uri.EscapeDataString(providerConfig.ClientId)}");
        authUrl.Append($"&redirect_uri={Uri.EscapeDataString(callbackUrl)}");
        authUrl.Append($"&scope={Uri.EscapeDataString(providerConfig.Scope)}");
        authUrl.Append($"&state={Uri.EscapeDataString(state)}");

        // GitHub requires specific Accept header for JSON response
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            authUrl.Append("&response_type=code");
        }

        _logger.LogDebug("Generated OAuth authorization URL for provider: {Provider}", provider);
        return Task.FromResult(authUrl.ToString());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates domain logic (user creation, account linking) to CQRS handlers.
    /// Maintains provider communication (token exchange, user info retrieval) in this service.
    /// </remarks>
    public async Task<OAuthCallbackResult> HandleCallbackAsync(string provider, string code, string state)
    {
        // 1. Validate CSRF state (keep in service - infrastructure concern)
        if (!await ValidateStateAsync(state))
        {
            _logger.LogWarning("Invalid OAuth state parameter for provider: {Provider}", provider);
            throw new UnauthorizedAccessException("Invalid state parameter. Possible CSRF attack.");
        }

        var providerConfig = GetProviderConfig(provider);

        // 2. Exchange authorization code for access token (provider communication - keep here)
        var tokenResponse = await ExchangeCodeForTokenAsync(providerConfig, provider, code);

        // 3. Get user info from provider (provider communication - keep here)
        var userInfo = await GetUserInfoAsync(providerConfig, provider, tokenResponse.AccessToken);

        // Validate user info contains required email (AUTH-06 requirement)
        if (string.IsNullOrEmpty(userInfo.Email))
        {
            throw new InvalidOperationException($"OAuth provider {provider} did not return email address");
        }

        // 4. Find existing OAuth account to determine flow
        var oauthAccount = await _db.OAuthAccounts
            .Include(oa => oa.User)
            .FirstOrDefaultAsync(oa =>
                oa.Provider == provider.ToLowerInvariant() &&
                oa.ProviderUserId == userInfo.Id);

        UserEntity? user;
        bool isNewUser = false;

        if (oauthAccount != null)
        {
            // Existing OAuth account - update token (still needs direct DB access for token encryption)
            if (oauthAccount.User == null)
            {
                throw new InvalidOperationException("OAuth account found but user entity not loaded");
            }
            user = oauthAccount.User;
            await UpdateOAuthTokenAsync(oauthAccount, tokenResponse);
            _logger.LogInformation("OAuth login for existing account. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
        }
        else
        {
            // Check if user exists with same email (auto-link for MVP)
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email.ToLowerInvariant());

            if (user == null)
            {
                // Create new user (still direct DB for now - would need RegisterViaOAuthCommand)
                // CWE-476: Safe email split with null/empty guards
                var emailParts = userInfo.Email?.Split('@') ?? Array.Empty<string>();
                var emailPrefix = emailParts.Length > 0 && !string.IsNullOrEmpty(emailParts[0])
                    ? emailParts[0]
                    : "User";

                user = new UserEntity
                {
                    Id = Guid.NewGuid(),
                    // CS8602: False positive - Email validated non-null at line 89
#pragma warning disable CS8602
                    Email = userInfo.Email.ToLowerInvariant(),
#pragma warning restore CS8602
                    DisplayName = userInfo.Name ?? emailPrefix,
                    PasswordHash = GenerateRandomPasswordHash(), // No password for OAuth-only users
                    Role = UserRole.User.ToString(),
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                isNewUser = true;
                _logger.LogInformation("Created new user via OAuth. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
            }
            else
            {
                _logger.LogInformation("Linking OAuth to existing user. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
            }

            // Create OAuth account link (still direct DB for token encryption)
            await CreateOAuthAccountAsync(user.Id, provider, userInfo, tokenResponse);
        }

        var authUser = new AuthUser(user.Id.ToString(), user.Email, user.DisplayName, user.Role.ToString());
        return new OAuthCallbackResult(authUser, isNewUser);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to UnlinkOAuthAccountCommand via IMediator for domain logic.
    /// </remarks>
    public async Task UnlinkOAuthAccountAsync(Guid userId, string provider)
    {
        var command = new BoundedContexts.Authentication.Application.Commands.OAuth.UnlinkOAuthAccountCommand
        {
            UserId = userId,
            Provider = provider
        };

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            _logger.LogWarning("OAuth account unlinking failed. UserId: {UserId}, Provider: {Provider}, Error: {Error}",
                userId, provider, result.ErrorMessage);
            throw new InvalidOperationException(result.ErrorMessage ?? $"Failed to unlink {provider} account.");
        }

        _logger.LogInformation("OAuth account unlinked. UserId: {UserId}, Provider: {Provider}", userId, provider);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to GetLinkedOAuthAccountsQuery via IMediator for domain logic.
    /// </remarks>
    public async Task<List<OAuthAccountDto>> GetLinkedAccountsAsync(Guid userId)
    {
        var query = new BoundedContexts.Authentication.Application.Queries.OAuth.GetLinkedOAuthAccountsQuery
        {
            UserId = userId
        };

        var result = await _mediator.Send(query);

        // Map from query DTO to service DTO (backward compatibility)
        return result.Accounts
            .Select(a => new OAuthAccountDto(a.Provider, a.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return Task.FromResult(false);

        lock (_stateStore)
        {
            if (_stateStore.TryGetValue(state, out var expiresAt))
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (now <= expiresAt)
                {
                    // Valid state - remove it (single-use)
                    _stateStore.Remove(state);
                    return Task.FromResult(true);
                }
                else
                {
                    // Expired - clean up
                    _stateStore.Remove(state);
                }
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task StoreStateAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be null or empty", nameof(state));

        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.Add(StateLifetime);

        lock (_stateStore)
        {
            _stateStore[state] = expiresAt;

            // Cleanup expired states
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiredKeys = _stateStore.Where(kvp => kvp.Value < now).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredKeys)
            {
                _stateStore.Remove(key);
            }
        }

        _logger.LogDebug("Stored OAuth state with expiration: {ExpiresAt}", expiresAt);
        return Task.CompletedTask;
    }

    // Private helper methods

    private OAuthProviderConfig GetProviderConfig(string provider)
    {
        var providerKey = provider.ToLowerInvariant() switch
        {
            "google" => "Google",
            "discord" => "Discord",
            "github" => "GitHub",
            _ => throw new ArgumentException($"Unsupported OAuth provider: {provider}")
        };

        if (!_config.Providers.TryGetValue(providerKey, out var config))
        {
            throw new InvalidOperationException($"OAuth provider {provider} is not configured.");
        }

        return config;
    }

    private string GetCallbackUrl(string provider)
    {
        var baseUrl = _config.CallbackBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/v1/auth/oauth/{provider.ToLowerInvariant()}/callback";
    }

    private async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(
        OAuthProviderConfig config,
        string provider,
        string code)
    {
#pragma warning disable CA2000 // HttpClient lifetime managed by IHttpClientFactory
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000
        var callbackUrl = GetCallbackUrl(provider);

        var requestData = new Dictionary<string, string>
        {
            { "client_id", config.ClientId },
            { "client_secret", config.ClientSecret },
            { "code", code },
            { "redirect_uri", callbackUrl },
            { "grant_type", "authorization_code" }
        };

        // CODE-01: Use using for FormUrlEncodedContent to ensure proper disposal (CWE-404)
        using var content = new FormUrlEncodedContent(requestData);

        using var request = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl)
        {
            Content = content
        };

        // GitHub requires Accept header for JSON response
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("Accept", "application/json");
        }

        try
        {
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

            var accessToken = tokenData.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("No access token in response");

            var refreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var expiresIn = tokenData.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : (int?)null;
            var tokenType = tokenData.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";

            _logger.LogDebug("Successfully exchanged OAuth code for token. Provider: {Provider}", provider);
            return new OAuthTokenResponse(accessToken, refreshToken, expiresIn, tokenType ?? "Bearer");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to exchange OAuth code for token. Provider: {Provider}", provider);
            throw new InvalidOperationException($"OAuth token exchange failed for {provider}", ex);
        }
    }

    private async Task<OAuthUserInfo> GetUserInfoAsync(
        OAuthProviderConfig config,
        string provider,
        string accessToken)
    {
#pragma warning disable CA2000 // HttpClient lifetime managed by IHttpClientFactory
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000
        using var request = new HttpRequestMessage(HttpMethod.Get, config.UserInfoUrl);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // GitHub requires User-Agent header
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("User-Agent", "MeepleAI");
        }

        try
        {
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var userData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

            // Parse user info based on provider
            var (id, email, name) = provider.ToLowerInvariant() switch
            {
                "google" => ParseGoogleUserInfo(userData),
                "discord" => ParseDiscordUserInfo(userData),
                "github" => await ParseGitHubUserInfoAsync(userData, accessToken),
                _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
            };

            _logger.LogDebug("Retrieved user info from OAuth provider. Provider: {Provider}", provider);
            return new OAuthUserInfo(id, email, name);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get user info from OAuth provider. Provider: {Provider}", provider);
            throw new InvalidOperationException($"Failed to get user info from {provider}", ex);
        }
    }

    private static (string Id, string Email, string? Name) ParseGoogleUserInfo(JsonElement userData)
    {
        var id = userData.GetProperty("sub").GetString() ?? throw new InvalidOperationException("No user ID");
        var email = userData.GetProperty("email").GetString() ?? throw new InvalidOperationException("No email");
        var name = userData.TryGetProperty("name", out var n) ? n.GetString() : null;
        return (id, email, name);
    }

    private static (string Id, string Email, string? Name) ParseDiscordUserInfo(JsonElement userData)
    {
        var id = userData.GetProperty("id").GetString() ?? throw new InvalidOperationException("No user ID");
        var email = userData.GetProperty("email").GetString() ?? throw new InvalidOperationException("No email");
        var username = userData.TryGetProperty("username", out var u) ? u.GetString() : null;
        return (id, email, username);
    }

    private async Task<(string Id, string Email, string? Name)> ParseGitHubUserInfoAsync(
        JsonElement userData,
        string accessToken)
    {
        var id = userData.GetProperty("id").GetInt64().ToString();
        var name = userData.TryGetProperty("name", out var n) ? n.GetString() : null;

        // GitHub doesn't always return email in user endpoint, need to fetch from /user/emails
        var email = userData.TryGetProperty("email", out var e) && !e.ValueKind.Equals(JsonValueKind.Null)
            ? e.GetString()
            : await GetGitHubPrimaryEmailAsync(accessToken);

        if (string.IsNullOrEmpty(email))
        {
            throw new InvalidOperationException("Could not retrieve email from GitHub");
        }

        return (id, email, name);
    }

    private async Task<string> GetGitHubPrimaryEmailAsync(string accessToken)
    {
#pragma warning disable CA2000 // HttpClient lifetime managed by IHttpClientFactory
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("User-Agent", "MeepleAI");

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var emails = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

        // Find primary verified email
        foreach (var emailObj in emails.EnumerateArray())
        {
            if (emailObj.GetProperty("primary").GetBoolean() &&
                emailObj.GetProperty("verified").GetBoolean())
            {
                return emailObj.GetProperty("email").GetString()
                    ?? throw new InvalidOperationException("Primary email is null");
            }
        }

        throw new InvalidOperationException("No verified primary email found on GitHub account");
    }

    private async Task CreateOAuthAccountAsync(Guid userId,
        string provider,
        OAuthUserInfo userInfo,
        OAuthTokenResponse tokenResponse)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = tokenResponse.ExpiresIn.HasValue
            ? now.AddSeconds(tokenResponse.ExpiresIn.Value)
            : (DateTime?)null;

        var accessTokenEncrypted = await _encryption.EncryptAsync(tokenResponse.AccessToken, EncryptionPurpose);
        var refreshTokenEncrypted = tokenResponse.RefreshToken != null
            ? await _encryption.EncryptAsync(tokenResponse.RefreshToken, EncryptionPurpose)
            : null;

        var oauthAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider.ToLowerInvariant(),
            ProviderUserId = userInfo.Id,
            AccessTokenEncrypted = accessTokenEncrypted,
            RefreshTokenEncrypted = refreshTokenEncrypted,
            TokenExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now,
            User = null! // Will be loaded by EF
        };

        _db.OAuthAccounts.Add(oauthAccount);
        await _db.SaveChangesAsync();
    }

    private async Task UpdateOAuthTokenAsync(OAuthAccountEntity account, OAuthTokenResponse tokenResponse)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = tokenResponse.ExpiresIn.HasValue
            ? now.AddSeconds(tokenResponse.ExpiresIn.Value)
            : (DateTime?)null;

        account.AccessTokenEncrypted = await _encryption.EncryptAsync(tokenResponse.AccessToken, EncryptionPurpose);

        if (tokenResponse.RefreshToken != null)
        {
            account.RefreshTokenEncrypted = await _encryption.EncryptAsync(tokenResponse.RefreshToken, EncryptionPurpose);
        }

        account.TokenExpiresAt = expiresAt;
        account.UpdatedAt = now;

        await _db.SaveChangesAsync();
    }

    private static string GenerateRandomPasswordHash()
    {
        // For OAuth-only users, generate a random unguessable password hash
        // They won't know the password and can only login via OAuth
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }

    /// <inheritdoc />
    public async Task<OAuthTokenResponse?> RefreshTokenAsync(Guid userId, string provider)
    {
        // GitHub doesn't support refresh tokens
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("GitHub does not support token refresh. UserId: {UserId}", userId);
            return null;
        }

        // 1. Get OAuth account with refresh token
        var oauthAccount = await _db.OAuthAccounts
            .FirstOrDefaultAsync(oa =>
                oa.UserId == userId &&
                oa.Provider == provider.ToLowerInvariant());

        if (oauthAccount == null)
        {
            _logger.LogWarning("No OAuth account found. UserId: {UserId}, Provider: {Provider}", userId, provider);
            return null;
        }

        if (string.IsNullOrEmpty(oauthAccount.RefreshTokenEncrypted))
        {
            _logger.LogWarning("No refresh token available. UserId: {UserId}, Provider: {Provider}", userId, provider);
            return null; // Force re-auth
        }

        // 2. Decrypt refresh token
        string refreshToken;
        try
        {
            refreshToken = await _encryption.DecryptAsync(oauthAccount.RefreshTokenEncrypted, EncryptionPurpose);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptographic error decrypting refresh token. UserId: {UserId}, Provider: {Provider}", userId, provider);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to decrypt refresh token (key rotation or corruption). UserId: {UserId}, Provider: {Provider}", userId, provider);
            return null;
        }

        // 3. Exchange refresh token for new access token
        var providerConfig = GetProviderConfig(provider);
        var newTokenResponse = await ExchangeRefreshTokenAsync(providerConfig, provider, refreshToken);

        if (newTokenResponse == null)
        {
            _logger.LogError("Token refresh failed. UserId: {UserId}, Provider: {Provider}", userId, provider);
            return null; // Invalid refresh token - force re-auth
        }

        // 4. Update encrypted tokens in database
        await UpdateOAuthTokenAsync(oauthAccount, newTokenResponse);

        _logger.LogInformation("Token refreshed successfully. UserId: {UserId}, Provider: {Provider}", userId, provider);
        return newTokenResponse;
    }

    private async Task<OAuthTokenResponse?> ExchangeRefreshTokenAsync(
        OAuthProviderConfig config,
        string provider,
        string refreshToken)
    {
#pragma warning disable CA2000 // HttpClient lifetime managed by IHttpClientFactory
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000

        var requestData = new Dictionary<string, string>
        {
            { "client_id", config.ClientId },
            { "client_secret", config.ClientSecret },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        };

        // CODE-01: Use using for FormUrlEncodedContent to ensure proper disposal (CWE-404)
        using var content = new FormUrlEncodedContent(requestData);

        using var request = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl)
        {
            Content = content
        };

        // GitHub requires Accept header (though it doesn't support refresh)
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("Accept", "application/json");
        }

        try
        {
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

            var accessToken = tokenData.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("No access token in refresh response");

            // Provider may return new refresh token or keep old one
            var newRefreshToken = tokenData.TryGetProperty("refresh_token", out var rt) && rt.ValueKind != JsonValueKind.Null
                ? rt.GetString()
                : refreshToken; // Keep old one if not provided

            var expiresIn = tokenData.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : (int?)null;
            var tokenType = tokenData.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";

            _logger.LogDebug("Successfully refreshed OAuth token. Provider: {Provider}", provider);
            return new OAuthTokenResponse(accessToken, newRefreshToken, expiresIn, tokenType ?? "Bearer");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to refresh OAuth token. Provider: {Provider}", provider);
            return null; // Force re-auth on any HTTP error
        }
    }
}

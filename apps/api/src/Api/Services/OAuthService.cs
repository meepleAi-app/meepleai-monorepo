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
/// OAuth 2.0 infrastructure adapter for Google, Discord, and GitHub.
/// Provides HTTP client operations for OAuth provider communication.
/// Business logic delegated to CQRS handlers (HandleOAuthCallbackCommand, UnlinkOAuthAccountCommand, etc).
/// This service is a pure infrastructure adapter for provider-specific HTTP operations.
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

    // Token encryption purpose for RefreshTokenAsync
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

    public async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(
        string provider,
        string code)
    {
        var config = GetProviderConfig(provider);
        return await ExchangeCodeForTokenInternalAsync(config, provider, code);
    }

    private async Task<OAuthTokenResponse> ExchangeCodeForTokenInternalAsync(
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

    public async Task<OAuthUserInfo> GetUserInfoAsync(
        string provider,
        string accessToken)
    {
        var config = GetProviderConfig(provider);
        return await GetUserInfoInternalAsync(config, provider, accessToken);
    }

    private async Task<OAuthUserInfo> GetUserInfoInternalAsync(
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

    /// <summary>
    /// Updates OAuth account tokens with encryption (infrastructure helper for RefreshTokenAsync)
    /// </summary>
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

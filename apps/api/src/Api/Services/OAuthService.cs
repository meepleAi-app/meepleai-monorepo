using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.Interfaces;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Api.Services;

/// <summary>
/// OAuth 2.0 infrastructure adapter for Google, Discord, and GitHub.
/// Provides HTTP client operations for OAuth provider communication.
/// Business logic delegated to CQRS handlers (HandleOAuthCallbackCommand, UnlinkOAuthAccountCommand, etc).
/// This service is a pure infrastructure adapter for provider-specific HTTP operations.
/// </summary>
internal class OAuthService : IOAuthService
{
    private readonly MeepleAiDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<OAuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthConfiguration _config;
    private readonly TimeProvider _timeProvider;
    private readonly IMediator _mediator;
    private readonly IOAuthStateStore _stateStore;

    // Token encryption purpose for RefreshTokenAsync
    private const string EncryptionPurpose = "OAuthTokens";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    public OAuthService(
        MeepleAiDbContext db,
        IEncryptionService encryption,
        ILogger<OAuthService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<OAuthConfiguration> config,
        IMediator mediator,
        IOAuthStateStore stateStore,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _encryption = encryption;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _mediator = mediator;
        _stateStore = stateStore;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<string> GetAuthorizationUrlAsync(string provider, string state)
    {
        var providerConfig = GetProviderConfig(provider);
        var callbackUrl = GetCallbackUrl(provider);

        // Ensure we don't end up with duplicate '?' when AuthorizationUrl already has query params
        var hasQuery = providerConfig.AuthorizationUrl.Contains('?');

        var authUrl = new StringBuilder(providerConfig.AuthorizationUrl);
        authUrl.Append(hasQuery ? "&" : "?");
        authUrl.Append("response_type=code");
        authUrl.Append($"&client_id={Uri.EscapeDataString(providerConfig.ClientId)}");
        authUrl.Append($"&redirect_uri={Uri.EscapeDataString(callbackUrl)}");
        authUrl.Append($"&scope={Uri.EscapeDataString(providerConfig.Scope)}");
        authUrl.Append($"&state={Uri.EscapeDataString(state)}");

        // Google needs explicit offline access and re-consent to issue refresh tokens
        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            authUrl.Append("&access_type=offline&prompt=consent");
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

        var result = await _mediator.Send(command).ConfigureAwait(false);

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

        var result = await _mediator.Send(query).ConfigureAwait(false);

        // Map from query DTO to service DTO (backward compatibility)
        return result.Accounts
            .Select(a => new OAuthAccountDto(a.Provider, a.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> ValidateStateAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return false;

        // Delegate to Redis-backed state store (single-use validation)
        return await _stateStore.ValidateAndRemoveStateAsync(state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StoreStateAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be null or empty", nameof(state));

        // Delegate to Redis-backed state store with TTL
        await _stateStore.StoreStateAsync(state, StateLifetime).ConfigureAwait(false);

        _logger.LogDebug("Stored OAuth state with {Lifetime} expiration", StateLifetime);
    }

    // Private helper methods

    private OAuthProviderConfig GetProviderConfig(string provider)
    {
        var providerKey = provider.ToLowerInvariant() switch
        {
            "google" => "Google",
            "discord" => "Discord",
            "github" => "GitHub",
            _ => throw new ArgumentException($"Unsupported OAuth provider: {provider}", nameof(provider))
        };

        if (!_config.Providers.TryGetValue(providerKey, out var config))
        {
            throw new InvalidOperationException($"OAuth provider {provider} is not configured.");
        }

        return NormalizeProviderConfig(providerKey, config);
    }

    private static OAuthProviderConfig NormalizeProviderConfig(string providerKey, OAuthProviderConfig config)
    {
        // Allow ${ENV_VAR} placeholders in appsettings to be resolved at runtime
        string Resolve(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}'))
            {
                var envName = value.Substring(2, value.Length - 3);
                var envValue = Environment.GetEnvironmentVariable(envName);
                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    return envValue;
                }

                throw new InvalidOperationException(
                    $"OAuth {providerKey} {fieldName} is configured as placeholder {value} but environment variable {envName} is not set.");
            }

            return value;
        }

        return new OAuthProviderConfig
        {
            ClientId = Resolve(config.ClientId, "ClientId"),
            ClientSecret = Resolve(config.ClientSecret, "ClientSecret"),
            AuthorizationUrl = config.AuthorizationUrl,
            TokenUrl = config.TokenUrl,
            UserInfoUrl = config.UserInfoUrl,
            Scope = config.Scope
        };
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
        return await ExchangeCodeForTokenInternalAsync(config, provider, code).ConfigureAwait(false);
    }

    private async Task<OAuthTokenResponse> ExchangeCodeForTokenInternalAsync(
        OAuthProviderConfig config,
        string provider,
        string code)
    {
        // CA2000 suppression: HttpClient from IHttpClientFactory MUST NOT be disposed manually.
        // The factory manages HttpMessageHandler pooling and lifetime. See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
#pragma warning disable CA2000 // Dispose objects before losing scope - False positive: IHttpClientFactory manages HttpClient lifetime
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
        var callbackUrl = GetCallbackUrl(provider);

        var requestData = new Dictionary<string, string>
(StringComparer.Ordinal)
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
            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
        return await GetUserInfoInternalAsync(config, provider, accessToken).ConfigureAwait(false);
    }

    private async Task<OAuthUserInfo> GetUserInfoInternalAsync(
        OAuthProviderConfig config,
        string provider,
        string accessToken)
    {
        // CA2000 suppression: HttpClient from IHttpClientFactory MUST NOT be disposed manually.
        // The factory manages HttpMessageHandler pooling and lifetime. See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
#pragma warning disable CA2000 // Dispose objects before losing scope - False positive: IHttpClientFactory manages HttpClient lifetime
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var request = new HttpRequestMessage(HttpMethod.Get, config.UserInfoUrl);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // GitHub requires User-Agent header
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("User-Agent", "MeepleAI");
        }

        try
        {
            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var userData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

            // Parse user info based on provider
            var (id, email, name) = provider.ToLowerInvariant() switch
            {
                "google" => ParseGoogleUserInfo(userData),
                "discord" => ParseDiscordUserInfo(userData),
                "github" => await ParseGitHubUserInfoAsync(userData, accessToken).ConfigureAwait(false),
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
        var id = userData.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);
        var name = userData.TryGetProperty("name", out var n) ? n.GetString() : null;

        // GitHub doesn't always return email in user endpoint, need to fetch from /user/emails
        var email = userData.TryGetProperty("email", out var e) && !e.ValueKind.Equals(JsonValueKind.Null)
            ? e.GetString()
            : await GetGitHubPrimaryEmailAsync(accessToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(email))
        {
            throw new InvalidOperationException("Could not retrieve email from GitHub");
        }

        return (id, email, name);
    }

    private async Task<string> GetGitHubPrimaryEmailAsync(string accessToken)
    {
        // S1075: GitHub API endpoint (official public endpoint)
        const string GitHubUserEmailsApiUrl = "https://api.github.com/user/emails";

        // CA2000 suppression: HttpClient from IHttpClientFactory MUST NOT be disposed manually.
        // The factory manages HttpMessageHandler pooling and lifetime. See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
#pragma warning disable CA2000 // Dispose objects before losing scope - False positive: IHttpClientFactory manages HttpClient lifetime
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var request = new HttpRequestMessage(HttpMethod.Get, GitHubUserEmailsApiUrl);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("User-Agent", "MeepleAI");

        using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        account.AccessTokenEncrypted = await _encryption.EncryptAsync(tokenResponse.AccessToken, EncryptionPurpose).ConfigureAwait(false);

        if (tokenResponse.RefreshToken != null)
        {
            account.RefreshTokenEncrypted = await _encryption.EncryptAsync(tokenResponse.RefreshToken, EncryptionPurpose).ConfigureAwait(false);
        }

        account.TokenExpiresAt = expiresAt;
        account.UpdatedAt = now;

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OAuthTokenResponse?> RefreshTokenAsync(Guid userId, string provider)
    {
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));
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
                oa.Provider == provider.ToLowerInvariant()).ConfigureAwait(false);

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
            refreshToken = await _encryption.DecryptAsync(oauthAccount.RefreshTokenEncrypted, EncryptionPurpose).ConfigureAwait(false);
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
        var newTokenResponse = await ExchangeRefreshTokenAsync(providerConfig, provider, refreshToken).ConfigureAwait(false);

        if (newTokenResponse == null)
        {
            _logger.LogError("Token refresh failed. UserId: {UserId}, Provider: {Provider}", userId, provider);
            return null; // Invalid refresh token - force re-auth
        }

        // 4. Update encrypted tokens in database
        await UpdateOAuthTokenAsync(oauthAccount, newTokenResponse).ConfigureAwait(false);

        _logger.LogInformation("Token refreshed successfully. UserId: {UserId}, Provider: {Provider}", userId, provider);
        return newTokenResponse;
    }

    private async Task<OAuthTokenResponse?> ExchangeRefreshTokenAsync(
        OAuthProviderConfig config,
        string provider,
        string refreshToken)
    {
        // CA2000 suppression: HttpClient from IHttpClientFactory MUST NOT be disposed manually.
        // The factory manages HttpMessageHandler pooling and lifetime. See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
#pragma warning disable CA2000 // Dispose objects before losing scope - False positive: IHttpClientFactory manages HttpClient lifetime
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope

        var requestData = new Dictionary<string, string>
(StringComparer.Ordinal)
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
            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

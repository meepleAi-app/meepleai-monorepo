using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Secrets;

/// <summary>
/// Infisical secrets management REST API client for POC (Issue #936).
/// Demonstrates secret fetching, versioning, and rotation capabilities.
/// Uses direct REST API for maximum control and simplicity.
/// </summary>
internal class InfisicalSecretsClient : IInfisicalClient
{
    private readonly HttpClient _httpClient;
    private readonly InfisicalOptions _options;
    private readonly ILogger<InfisicalSecretsClient> _logger;
    private string? _accessToken;
    private DateTime _tokenExpiresAt;

    public InfisicalSecretsClient(
        IHttpClientFactory httpClientFactory,
        IOptions<InfisicalOptions> options,
        ILogger<InfisicalSecretsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClientFactory.CreateClient("Infisical");
        _options = options.Value ?? throw new ArgumentException("Infisical options value is missing", nameof(options));
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.HostUrl))
        {
            throw new ArgumentException("Infisical HostUrl is not configured", nameof(options));
        }

        // Configure base URL
        _httpClient.BaseAddress = new Uri(_options.HostUrl);
    }

    public async Task<string> GetSecretAsync(
        string secretName,
        string environment = "dev",
        string secretPath = "/",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secretName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(secretPath);

        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _logger.LogDebug(
                "Fetching secret {SecretName} from {Environment} at {Path}",
                secretName, environment, secretPath);

            var url = $"/api/v3/secrets/raw/{secretName}" +
                      $"?workspaceId={_options.ProjectId}" +
                      $"&environment={environment}" +
                      $"&secretPath={Uri.EscapeDataString(secretPath)}";

            var token = _accessToken ?? throw new InvalidOperationException("Infisical client not authenticated");
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var secretResponse = await response.Content
                .ReadFromJsonAsync<InfisicalSecretResponse>(cancellationToken).ConfigureAwait(false);

            if (secretResponse?.Secret == null)
            {
                throw new InvalidOperationException(
                    $"Secret {secretName} not found or invalid response");
            }

            _logger.LogInformation(
                "Retrieved secret {SecretName} (v{Version})",
                secretName, secretResponse.Secret.Version);

            return secretResponse.Secret.SecretValue;
        }
        catch (HttpRequestException ex)
        {
            // Capture specific status code context if needed, but rely on upstream logging to avoid duplication (S2139)
            // Rethrowing allows the caller (e.g. startup) to handle the failure appropriately
            throw new InvalidOperationException($"HTTP error fetching secret {secretName}: {ex.StatusCode}", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: EXCEPTION WRAPPING PATTERN - Domain boundary
        // Wraps infrastructure exceptions (JSON, network, unexpected) into domain exception.
        // Preserves inner exception for debugging. HttpRequestException caught separately.
#pragma warning restore S125
        catch (Exception ex)
        {
            // Wrap in a more meaningful exception for the domain
            throw new InvalidOperationException($"Failed to fetch secret {secretName}", ex);
        }
#pragma warning restore CA1031
    }

    public async Task<SecretVersion[]> GetSecretVersionsAsync(
        string secretName,
        string environment = "dev",
        string secretPath = "/",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secretName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(secretPath);

        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _logger.LogDebug(
                "Fetching version history for {SecretName}",
                secretName);

            var url = $"/api/v3/secrets/{secretName}/secret-versions" +
                      $"?workspaceId={_options.ProjectId}" +
                      $"&environment={environment}" +
                      $"&secretPath={Uri.EscapeDataString(secretPath)}";

            var token = _accessToken ?? throw new InvalidOperationException("Infisical client not authenticated");
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var versionResponse = await response.Content
                .ReadFromJsonAsync<InfisicalVersionsResponse>(cancellationToken).ConfigureAwait(false);

            if (versionResponse?.SecretVersions == null || versionResponse.SecretVersions.Length == 0)
            {
                _logger.LogWarning(
                    "No version history found for secret {SecretName}",
                    secretName);
                return Array.Empty<SecretVersion>();
            }

            var versions = versionResponse.SecretVersions
                .Select(v => new SecretVersion(
                    v.Version,
                    v.SecretValue,
                    v.CreatedAt,
                    v.CreatedBy))
                .ToArray();

            _logger.LogInformation(
                "Retrieved {VersionCount} versions for secret {SecretName}",
                versions.Length, secretName);

            return versions;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Version history not supported or secret {SecretName} not found", secretName);
            return Array.Empty<SecretVersion>();
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: EXCEPTION WRAPPING PATTERN - Domain boundary
        // Wraps infrastructure exceptions into domain exception. Preserves inner exception.
        // HttpRequestException with NotFound caught separately above.
#pragma warning restore S125
        catch (Exception ex)
        {
            // S2139: Do not log and rethrow.
            throw new InvalidOperationException($"Failed to fetch version history for {secretName}", ex);
        }
#pragma warning restore CA1031
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing Infisical health check");

            await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

            // Test connection by fetching project info
            var url = $"/api/v1/workspace/{_options.ProjectId}";

            var token = _accessToken ?? throw new InvalidOperationException("Infisical client not authenticated");
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Infisical health check passed");
                return true;
            }

            _logger.LogWarning(
                "Infisical health check failed: {StatusCode}",
                response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Infisical health check failed - HTTP error");
            return false; // Health check failure, return false
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Infisical health check timed out");
            return false; // Timeout is unhealthy
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: HEALTH CHECK PATTERN - Non-critical monitoring
        // Health check failures return false; don't throw. Specific HTTP/timeout caught separately.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Infisical health check");
            return false; // Unexpected errors indicate unhealthy state
        }
#pragma warning restore CA1031
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        // Check if token exists and is not expired (with 5min buffer)
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
        {
            return; // Token still valid
        }

        try
        {
            _logger.LogInformation("Authenticating with Infisical Universal Auth");

            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                throw new InvalidOperationException("Infisical client credentials are not configured");
            }

            var loginRequest = new
            {
                clientId = _options.ClientId,
                clientSecret = _options.ClientSecret
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/auth/universal-auth/login",
                loginRequest,
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var authResponse = await response.Content
                .ReadFromJsonAsync<InfisicalAuthResponse>(cancellationToken).ConfigureAwait(false);

            if (authResponse?.AccessToken == null)
            {
                throw new InvalidOperationException("Invalid auth response from Infisical");
            }

            _accessToken = authResponse.AccessToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(authResponse.ExpiresIn);

            _logger.LogInformation(
                "Successfully authenticated with Infisical (token expires at {ExpiresAt})",
                _tokenExpiresAt);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both - Intentional: log for diagnostics AND propagate for caller handling
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error authenticating with Infisical");
            throw; // Rethrow - authentication failure prevents secret access
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout authenticating with Infisical");
            throw; // Rethrow - authentication timeout prevents secret access
        }
#pragma warning restore S2139
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: EXCEPTION WRAPPING PATTERN - Authentication boundary
        // Wraps infrastructure exceptions into domain exception. Specific HTTP/timeout caught separately.
        // Authentication failures must propagate to prevent secret access with invalid credentials.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error authenticating with Infisical");
            throw new InvalidOperationException("Failed to authenticate with Infisical", ex);
        }
#pragma warning restore CA1031
    }
}

// DTOs for Infisical REST API responses
internal record InfisicalAuthResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("accessTokenMaxTTL")] int AccessTokenMaxTTL,
    [property: JsonPropertyName("tokenType")] string TokenType);

internal record InfisicalSecretResponse(
    [property: JsonPropertyName("secret")] InfisicalSecret Secret);

internal record InfisicalSecret(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("workspace")] string Workspace,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("secretKey")] string SecretKey,
    [property: JsonPropertyName("secretValue")] string SecretValue,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);

internal record InfisicalVersionsResponse(
    [property: JsonPropertyName("secretVersions")] InfisicalSecretVersionDto[] SecretVersions);

internal record InfisicalSecretVersionDto(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("secretValue")] string SecretValue,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("createdBy")] string? CreatedBy);
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
public class InfisicalSecretsClient : IInfisicalClient
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
        _httpClient = httpClientFactory.CreateClient("Infisical");
        _options = options.Value;
        _logger = logger;

        // Configure base URL
        _httpClient.BaseAddress = new Uri(_options.HostUrl);
    }

    public async Task<string> GetSecretAsync(
        string secretName,
        string environment = "dev",
        string secretPath = "/",
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        try
        {
            _logger.LogDebug(
                "Fetching secret {SecretName} from {Environment} at {Path}",
                secretName, environment, secretPath);

            var url = $"/api/v3/secrets/raw/{secretName}" +
                      $"?workspaceId={_options.ProjectId}" +
                      $"&environment={environment}" +
                      $"&secretPath={Uri.EscapeDataString(secretPath)}";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var secretResponse = await response.Content
                .ReadFromJsonAsync<InfisicalSecretResponse>(cancellationToken);

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
            _logger.LogError(ex,
                "HTTP error fetching secret {SecretName}: {StatusCode}",
                secretName, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch secret {SecretName}",
                secretName);
            throw;
        }
    }

    public async Task<SecretVersion[]> GetSecretVersionsAsync(
        string secretName,
        string environment = "dev",
        string secretPath = "/",
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        try
        {
            _logger.LogDebug(
                "Fetching version history for {SecretName}",
                secretName);

            var url = $"/api/v3/secrets/{secretName}/secret-versions" +
                      $"?workspaceId={_options.ProjectId}" +
                      $"&environment={environment}" +
                      $"&secretPath={Uri.EscapeDataString(secretPath)}";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var versionResponse = await response.Content
                .ReadFromJsonAsync<InfisicalVersionsResponse>(cancellationToken);

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
            _logger.LogWarning("Version history not supported or secret {SecretName} not found", secretName);
            return Array.Empty<SecretVersion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch version history for {SecretName}",
                secretName);
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing Infisical health check");

            await EnsureAuthenticatedAsync(cancellationToken);

            // Test connection by fetching project info
            var url = $"/api/v1/workspace/{_options.ProjectId}";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync(url, cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Infisical health check exception");
            return false;
        }
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

            var loginRequest = new
            {
                clientId = _options.ClientId,
                clientSecret = _options.ClientSecret
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/auth/universal-auth/login",
                loginRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var authResponse = await response.Content
                .ReadFromJsonAsync<InfisicalAuthResponse>(cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Infisical authentication failed");
            throw new InvalidOperationException(
                "Failed to authenticate with Infisical. Check CLIENT_ID and CLIENT_SECRET.", ex);
        }
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

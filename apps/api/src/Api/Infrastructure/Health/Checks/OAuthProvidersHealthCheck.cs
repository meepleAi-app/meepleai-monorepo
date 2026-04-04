using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for OAuth provider configuration (Google, Discord).
/// Validates that required OAuth credentials are present.
/// </summary>
public class OAuthProvidersHealthCheck : IHealthCheck
{
    private readonly ILogger<OAuthProvidersHealthCheck> _logger;

    public OAuthProvidersHealthCheck(
        ILogger<OAuthProvidersHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var missingProviders = new List<string>();

        // Check OAuth providers via environment variables (loaded by SecretLoader)
        string[] providers = ["Google", "Discord", "GitHub"];
        foreach (var provider in providers)
        {
            var clientId = Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant()}_OAUTH_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant()}_OAUTH_CLIENT_SECRET");
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                missingProviders.Add(provider);
            }
        }

        if (missingProviders.Count > 0)
        {
            var providersList = string.Join(", ", missingProviders);
            var message = $"OAuth providers not configured: {providersList}";
            _logger.LogWarning("OAuth providers not configured: {Providers}", providersList);
            return Task.FromResult(HealthCheckResult.Degraded(message));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All OAuth providers configured"));
    }
}

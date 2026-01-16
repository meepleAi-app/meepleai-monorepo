using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for OAuth provider configuration (Google, Discord).
/// Validates that required OAuth credentials are present.
/// </summary>
public class OAuthProvidersHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OAuthProvidersHealthCheck> _logger;

    public OAuthProvidersHealthCheck(
        IConfiguration configuration,
        ILogger<OAuthProvidersHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var missingProviders = new List<string>();

        // Check Google OAuth
        var googleClientId = _configuration["Authentication:Google:ClientId"];
        var googleClientSecret = _configuration["Authentication:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
        {
            missingProviders.Add("Google");
        }

        // Check Discord OAuth
        var discordClientId = _configuration["Authentication:Discord:ClientId"];
        var discordClientSecret = _configuration["Authentication:Discord:ClientSecret"];
        if (string.IsNullOrWhiteSpace(discordClientId) || string.IsNullOrWhiteSpace(discordClientSecret))
        {
            missingProviders.Add("Discord");
        }

        if (missingProviders.Count > 0)
        {
            var message = $"OAuth providers not configured: {string.Join(", ", missingProviders)}";
            _logger.LogWarning(message);
            return Task.FromResult(HealthCheckResult.Degraded(message));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All OAuth providers configured"));
    }
}

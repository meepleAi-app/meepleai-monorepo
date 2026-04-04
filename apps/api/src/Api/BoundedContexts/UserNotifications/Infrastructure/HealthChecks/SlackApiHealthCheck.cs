using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;

/// <summary>
/// Health check that verifies Slack API reachability by calling the api.test endpoint.
/// Best-effort check with 5-second timeout. Returns Healthy if 200 OK, Unhealthy otherwise.
/// </summary>
internal class SlackApiHealthCheck : IHealthCheck
{
    private static readonly Uri SlackApiTestUri = new("https://slack.com/api/api.test");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackApiHealthCheck> _logger;

    public SlackApiHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<SlackApiHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            using var response = await client.PostAsync(SlackApiTestUri, null, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Slack API is reachable");
            }

            _logger.LogWarning(
                "Slack API health check returned {StatusCode}",
                (int)response.StatusCode);

            return HealthCheckResult.Unhealthy(
                $"Slack API returned HTTP {(int)response.StatusCode}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Slack API health check timed out (>5s)");
            return HealthCheckResult.Unhealthy("Slack API health check timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Slack API health check failed - HTTP error");
            return HealthCheckResult.Unhealthy("Slack API unreachable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slack API health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Slack API health check failed", ex);
        }
    }
}

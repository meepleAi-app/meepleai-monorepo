using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Email/SMTP server connectivity.
/// </summary>
public class EmailSmtpHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSmtpHealthCheck> _logger;

    public EmailSmtpHealthCheck(
        IConfiguration configuration,
        ILogger<EmailSmtpHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Read from environment variables (loaded by SecretLoader from email.secret)
        var smtpServer = Environment.GetEnvironmentVariable("SMTP_HOST")
                         ?? _configuration["Email:SmtpServer"];
        var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT")
                          ?? _configuration["Email:Port"];

        if (string.IsNullOrWhiteSpace(smtpServer) || !int.TryParse(smtpPortStr, System.Globalization.CultureInfo.InvariantCulture, out var smtpPort))
        {
            return HealthCheckResult.Degraded("SMTP server not configured");
        }

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            await client.ConnectAsync(smtpServer, smtpPort, cts.Token).ConfigureAwait(false);

            return client.Connected
                ? HealthCheckResult.Healthy($"SMTP server {smtpServer}:{smtpPort} is reachable")
                : HealthCheckResult.Degraded($"SMTP server {smtpServer}:{smtpPort} unreachable");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "SMTP health check timeout (>5s) for {Server}:{Port}", smtpServer, smtpPort);
            return HealthCheckResult.Degraded($"Timeout checking SMTP server {smtpServer}:{smtpPort}");
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "SMTP health check failed - socket error for {Server}:{Port}", smtpServer, smtpPort);
            return HealthCheckResult.Unhealthy($"SMTP server {smtpServer}:{smtpPort} unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP health check failed - unexpected error for {Server}:{Port}", smtpServer, smtpPort);
            return HealthCheckResult.Unhealthy("SMTP connectivity check failed", ex);
        }
    }
}

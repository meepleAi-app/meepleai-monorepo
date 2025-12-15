using Api.Extensions;
using Api.Models;
using Api.Services;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Alert management endpoints.
/// Handles Prometheus webhook integration, alert retrieval, and manual alert resolution.
/// </summary>
internal static class AlertEndpoints
{
    public static RouteGroupBuilder MapAlertEndpoints(this RouteGroupBuilder group)
    {
        // OPS-07: Alerting system endpoints

        // Prometheus AlertManager webhook endpoint (no auth - called by Prometheus)
        group.MapPost("/alerts/prometheus", async (
            IAlertingService alertingService,
            PrometheusAlertWebhook webhook,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            logger.LogInformation(
                "Received Prometheus webhook: {Status}, {AlertCount} alerts",
                webhook.Status,
                webhook.Alerts.Length);

            // Process each alert independently - one failure must not block others
            foreach (var alert in webhook.Alerts)
            {
                try
                {
                    if (string.Equals(alert.Status, "firing", StringComparison.Ordinal))
                    {
                        var metadata = new Dictionary<string, object>
(StringComparer.Ordinal)
                        {
                            ["labels"] = alert.Labels,
                            ["annotations"] = alert.Annotations,
                            ["starts_at"] = alert.StartsAt,
                            ["group_key"] = webhook.GroupKey
                        };

                        await alertingService.SendAlertAsync(
                            alertType: alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                            severity: alert.Labels.GetValueOrDefault("severity", "warning"),
                            message: alert.Annotations.GetValueOrDefault("summary", "Alert triggered"),
                            metadata: metadata,
                            cancellationToken: ct).ConfigureAwait(false);
                    }
                    else if (string.Equals(alert.Status, "resolved", StringComparison.Ordinal))
                    {
                        var alertType = alert.Labels.GetValueOrDefault("alertname", "Unknown");
                        await alertingService.ResolveAlertAsync(alertType, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue processing remaining alerts
                    logger.LogError(ex, "Failed to process Prometheus alert: {AlertName}",
                        alert.Labels.GetValueOrDefault("alertname", "Unknown"));
                }
            }

            return Results.Ok(new { message = "Webhook processed successfully" });
        })
        .WithName("PrometheusAlertWebhook")
        .WithTags("Alerting")
        .WithDescription("Webhook endpoint for Prometheus AlertManager (no auth required)")
        .Produces(StatusCodes.Status200OK);

        // Admin endpoint to get active alerts
        group.MapGet("/admin/alerts", async (
            HttpContext context,
            IAlertingService alertingService,
            bool? activeOnly = true,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var alerts = activeOnly == true
                ? await alertingService.GetActiveAlertsAsync(ct)
.ConfigureAwait(false) : await alertingService.GetAlertHistoryAsync(
                    DateTime.UtcNow.AddDays(-7),
                    DateTime.UtcNow,
                    ct).ConfigureAwait(false);

            return Results.Ok(alerts);
        })
        .WithName("GetAlerts")
        .WithTags("Admin", "Alerting")
        .WithDescription("Get active alerts or recent alert history (admin only)")
        .Produces<List<AlertDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Admin endpoint to manually resolve an alert
        group.MapPost("/admin/alerts/{alertType}/resolve", async (
            HttpContext context,
            IAlertingService alertingService,
            string alertType,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var resolved = await alertingService.ResolveAlertAsync(alertType, ct).ConfigureAwait(false);

            if (!resolved)
            {
                return Results.NotFound(new { error = $"No active alerts found for type '{alertType}'" });
            }

            return Results.Ok(new { message = $"Alert '{alertType}' resolved successfully" });
        })
        .WithName("ResolveAlert")
        .WithTags("Admin", "Alerting")
        .WithDescription("Manually resolve an alert by type (admin only)")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return group;
    }
}

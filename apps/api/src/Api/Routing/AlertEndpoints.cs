using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using Api.Models;
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
        MapPrometheusWebhookEndpoint(group);
        MapAdminGetAlertsEndpoint(group);
        MapAdminResolveAlertEndpoint(group);

        return group;
    }

    private static void MapPrometheusWebhookEndpoint(RouteGroupBuilder group)
    {
        // Prometheus AlertManager webhook endpoint (no auth - called by Prometheus)
        group.MapPost("/alerts/prometheus", ProcessPrometheusWebhookAsync)
        .WithName("PrometheusAlertWebhook")
        .WithTags("Alerting")
        .WithDescription("Webhook endpoint for Prometheus AlertManager (no auth required)")
        .Produces(StatusCodes.Status200OK);
    }

    private static async Task<IResult> ProcessPrometheusWebhookAsync(
        IMediator mediator,
        PrometheusAlertWebhook webhook,
        ILogger<Program> logger,
        CancellationToken ct)
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
                    var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["labels"] = alert.Labels,
                        ["annotations"] = alert.Annotations,
                        ["starts_at"] = alert.StartsAt,
                        ["group_key"] = webhook.GroupKey
                    };

                    await mediator.Send(new SendAlertCommand(
                        AlertType: alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                        Severity: alert.Labels.GetValueOrDefault("severity", "warning"),
                        Message: alert.Annotations.GetValueOrDefault("summary", "Alert triggered"),
                        Metadata: metadata
                    ), ct).ConfigureAwait(false);
                }
                else if (string.Equals(alert.Status, "resolved", StringComparison.Ordinal))
                {
                    var alertType = alert.Labels.GetValueOrDefault("alertname", "Unknown");
                    await mediator.Send(new ResolveAlertCommand(alertType), ct).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                // Log but continue processing remaining alerts
                logger.LogError(ex, "Failed to process Prometheus alert: {AlertName}",
                    alert.Labels.GetValueOrDefault("alertname", "Unknown"));
            }
#pragma warning restore CA1031
        }

        return Results.Ok(new { message = "Webhook processed successfully" });
    }

    private static void MapAdminGetAlertsEndpoint(RouteGroupBuilder group)
    {
        // Admin endpoint to get active alerts
        group.MapGet("/admin/alerts", async (
            HttpContext context,
            IMediator mediator,
            bool? activeOnly = true,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var alerts = activeOnly.GetValueOrDefault(true)
                ? await mediator.Send(new GetActiveAlertsQuery(), ct).ConfigureAwait(false)
                : await mediator.Send(new GetAlertHistoryQuery(
                    DateTime.UtcNow.AddDays(-7),
                    DateTime.UtcNow
                ), ct).ConfigureAwait(false);

            return Results.Ok(alerts);
        })
        .WithName("GetAlerts")
        .WithTags("Admin", "Alerting")
        .WithDescription("Get active alerts or recent alert history (admin only)")
        .Produces<List<AlertDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapAdminResolveAlertEndpoint(RouteGroupBuilder group)
    {
        // Admin endpoint to manually resolve an alert
        group.MapPost("/admin/alerts/{alertType}/resolve", async (
            HttpContext context,
            IMediator mediator,
            string alertType,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var resolved = await mediator.Send(new ResolveAlertCommand(alertType), ct).ConfigureAwait(false);

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
    }
}

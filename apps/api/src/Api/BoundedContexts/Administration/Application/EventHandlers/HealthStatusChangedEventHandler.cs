using Api.BoundedContexts.Administration.Domain.Events;
using Api.Infrastructure.Health.Models;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Handles HealthStatusChangedEvent by forwarding alerts to the alerting system.
/// Maps health check tags and service names to alert categories and severity levels,
/// enabling multi-channel routing (e.g., critical alerts to #alerts-critical Slack channel).
/// </summary>
internal sealed class HealthStatusChangedEventHandler
    : INotificationHandler<HealthStatusChangedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthStatusChangedEventHandler> _logger;

    public HealthStatusChangedEventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthStatusChangedEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Handle(HealthStatusChangedEvent evt, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertingService = scope.ServiceProvider.GetRequiredService<IAlertingService>();

        var category = MapCategory(evt.ServiceName, evt.Tags);
        var severity = MapSeverity(evt.CurrentStatus);
        var alertType = evt.IsReminder
            ? TruncateAlertType($"health.reminder.{evt.ServiceName}")
            : TruncateAlertType($"health.{evt.ServiceName}");

        var message = evt.IsReminder
            ? $"REMINDER: {evt.ServiceName} is still {evt.CurrentStatus}. {evt.Description}"
            : $"{evt.ServiceName}: {evt.PreviousStatus} → {evt.CurrentStatus}. {evt.Description}";

        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["service"] = evt.ServiceName,
            ["previous_status"] = evt.PreviousStatus,
            ["current_status"] = evt.CurrentStatus,
            ["is_reminder"] = evt.IsReminder,
            ["_slack_category"] = category
        };

        try
        {
            await alertingService.SendAlertAsync(alertType, severity, message, metadata, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "[HealthAlert] {AlertType} ({Severity}) → category={Category}",
                alertType, severity, category);
        }
        catch (InvalidOperationException ex)
        {
            // AlertingService throws when disabled — log and continue, don't break DB persist
            _logger.LogDebug(ex, "[HealthAlert] Alerting disabled, skipping {AlertType}", alertType);
        }
    }

    internal static string MapCategory(string serviceName, string[] tags)
    {
        if (string.Equals(serviceName, "oauth", StringComparison.OrdinalIgnoreCase))
            return "security";

        if (tags.Contains(HealthCheckTags.Core, StringComparer.OrdinalIgnoreCase)
            || tags.Contains(HealthCheckTags.Critical, StringComparer.OrdinalIgnoreCase))
            return "infrastructure";
        if (tags.Contains(HealthCheckTags.Ai, StringComparer.OrdinalIgnoreCase))
            return "ai";
        if (tags.Contains(HealthCheckTags.External, StringComparer.OrdinalIgnoreCase))
            return "external";
        if (tags.Contains(HealthCheckTags.Monitoring, StringComparer.OrdinalIgnoreCase))
            return "monitoring";

        return "general";
    }

    internal static string MapSeverity(string currentStatus) => currentStatus switch
    {
        "Unhealthy" => "critical",
        "Degraded" => "warning",
        _ => "info"
    };

    private static string TruncateAlertType(string alertType) =>
        alertType.Length > 100 ? alertType[..100] : alertType;
}

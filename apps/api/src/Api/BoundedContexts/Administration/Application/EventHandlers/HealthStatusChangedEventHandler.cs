using Api.BoundedContexts.Administration.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.Infrastructure.Health.Models;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Issue #1941 / iso-2 Fix 2: per-service idempotency check. If we already dispatched
        // an alert for this exact event id (rolled-back/retried domain event), skip the Slack
        // send to avoid double-pinging oncall. Storage is a single row keyed by ServiceName —
        // upserted after each successful dispatch. Skip on InMemory (unit tests) — relational only.
        if (dbContext.Database.IsRelational())
        {
            var existing = await dbContext.HealthStatusAlertsSent
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ServiceName == evt.ServiceName, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null && existing.LastEventId == evt.EventId)
            {
                _logger.LogDebug(
                    "[HealthAlert] Skipping dispatch for service {ServiceName}: event {EventId} already sent at {SentAt} (iso-2)",
                    evt.ServiceName, evt.EventId, existing.LastSentAt);
                return;
            }
        }

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

        // Email is reserved for critical outages of CORE infrastructure only. Warning/info
        // transitions — and critical transitions of monitoring/non-critical/optional services
        // (grafana, prometheus, ollama, embedding…) — are routed to Slack/DB/dashboard but kept
        // OFF email to avoid alert fatigue. See ShouldSuppressEmail (#3245). The flag is scoped
        // to THIS alert, so budget/security/dead-letter warning emails from other producers are
        // unaffected. See EmailAlertChannel.IsEmailSuppressed.
        if (ShouldSuppressEmail(severity, evt.Tags))
        {
            metadata[EmailAlertChannel.SuppressEmailMetadataKey] = true;
        }

        try
        {
            await alertingService.SendAlertAsync(alertType, severity, message, metadata, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "[HealthAlert] {AlertType} ({Severity}) → category={Category}",
                alertType, severity, category);

            // Issue #1941 / iso-2 Fix 2: upsert the dedup record so a retried event short-circuits
            // at the pre-flight check above. Skip on InMemory (no relational provider in tests).
            if (dbContext.Database.IsRelational())
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO health_status_alerts_sent (service_name, last_event_id, last_sent_at)
                       VALUES ({evt.ServiceName}, {evt.EventId}, {DateTime.UtcNow})
                       ON CONFLICT (service_name) DO UPDATE
                       SET last_event_id = EXCLUDED.last_event_id,
                           last_sent_at = EXCLUDED.last_sent_at",
                    cancellationToken).ConfigureAwait(false);
            }
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

    /// <summary>
    /// Whether a health alert should be kept off the email channel. Two gates (#3245):
    /// <list type="number">
    /// <item>Only "critical" (an Unhealthy transition) is ever eligible for email;
    /// "warning" (Degraded) and "info" (recovery) are always suppressed.</item>
    /// <item>Among critical transitions, only genuinely critical infrastructure —
    /// services tagged <see cref="HealthCheckTags.Core"/> or <see cref="HealthCheckTags.Critical"/>
    /// (postgres, redis, pgvector) — emails. Monitoring, non-critical and optional services
    /// (grafana, prometheus, ollama, embedding, reranker…) route to Slack + DB + dashboard
    /// but NOT email, to avoid alert fatigue from outages that are not user-facing
    /// emergencies.</item>
    /// </list>
    /// Other channels (Slack, DB) are unaffected regardless.
    /// </summary>
    internal static bool ShouldSuppressEmail(string severity, string[] tags)
    {
        if (!string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isCriticalInfrastructure =
            tags is not null
            && (tags.Contains(HealthCheckTags.Core, StringComparer.OrdinalIgnoreCase)
                || tags.Contains(HealthCheckTags.Critical, StringComparer.OrdinalIgnoreCase));

        return !isCriticalInfrastructure;
    }

    private static string TruncateAlertType(string alertType) =>
        alertType.Length > 100 ? alertType[..100] : alertType;
}

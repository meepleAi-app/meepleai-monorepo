using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Raised when an <see cref="AlertRule"/> fires — either because a real metric
/// crossed its threshold or because an admin invoked the per-rule TestAlert
/// endpoint (Issue #1840 SP5 F4-C7).
///
/// <para>Consumed by:
/// <list type="bullet">
///   <item><c>ChannelDispatchHandler</c> — fans out to Email/Slack/etc.
///         Skips dispatch when <see cref="IsDryRun"/>=true.</item>
///   <item><c>DomainEventLogPersistenceHandler</c> — durable log row keyed by
///         the <c>alert.fired</c> alias registered in <c>EventTypeRegistry</c>.</item>
///   <item><c>IEventBroadcaster</c> (SSE) — drives the AlertActivityFeed live
///         in <c>/admin/monitor?tab=alerts</c>.</item>
/// </list>
/// </para>
///
/// <para>The <see cref="IsTest"/> flag distinguishes admin-triggered probes
/// from real threshold violations so the FE can render a "TEST" badge.
/// The <see cref="IsDryRun"/> flag toggles whether channels are actually invoked.</para>
/// </summary>
public sealed record AlertFiredEvent(
    Guid RuleId,
    string RuleName,
    string AlertType,
    string Metric,
    double Value,
    double Threshold,
    string ThresholdUnit,
    AlertSeverityKind Severity,
    IReadOnlyList<string> Channels,
    bool IsDryRun,
    bool IsTest,
    string TriggeredBy) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}

/// <summary>
/// Public mirror of <see cref="AlertSeverity"/> exposed via domain events.
/// The internal enum is intentionally not surfaced here because cross-BC
/// consumers (event log, SSE) should not depend on Administration internals.
/// </summary>
public enum AlertSeverityKind
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3,
}

internal static class AlertSeverityKindExtensions
{
    public static AlertSeverityKind ToKind(this AlertSeverity severity) => severity switch
    {
        AlertSeverity.Info => AlertSeverityKind.Info,
        AlertSeverity.Warning => AlertSeverityKind.Warning,
        AlertSeverity.Error => AlertSeverityKind.Error,
        AlertSeverity.Critical => AlertSeverityKind.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };
}

using Api.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Immutable state record representing the health status of a monitored service.
/// Used by <see cref="HealthStateMachine"/> for pure-function state transitions.
/// </summary>
public record ServiceHealthState(
    string ServiceName,
    string CurrentStatus,
    string PreviousStatus,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTime LastTransitionAt,
    DateTime? LastNotifiedAt,
    string? LastDescription);

/// <summary>
/// Pure-function state machine for health check status transitions.
/// Implements hysteresis to prevent flapping:
/// - Healthy → Degraded after DegradedThreshold consecutive failures (default: 3)
/// - Degraded → Unhealthy after UnhealthyThreshold total failures (default: 5)
/// - Degraded → Healthy on any success
/// - Unhealthy → Healthy after RecoveryThreshold consecutive successes (default: 2)
/// - Unhealthy + failure resets consecutive success counter
///
/// The DegradedThreshold hysteresis-in absorbs transient single-poll blips
/// (external API slow, ML cold-start, FTS latency spike) that would otherwise flap
/// Healthy↔Degraded and spam warning alerts for non-critical services.
/// </summary>
internal static class HealthStateMachine
{
    /// <summary>
    /// Evaluates a health check result against the current state and returns a new state.
    /// This is a pure function with no side effects.
    /// </summary>
    public static ServiceHealthState Evaluate(
        ServiceHealthState current,
        HealthStatus checkResult,
        HealthMonitorOptions options)
    {
        var isFailure = checkResult != HealthStatus.Healthy;
        var now = DateTime.UtcNow;

        return current.CurrentStatus switch
        {
            "Healthy" when isFailure =>
                HandleHealthyFailure(current, checkResult, options, now),

            "Healthy" => current with
            {
                PreviousStatus = "Healthy",
                ConsecutiveFailures = 0
            },

            "Degraded" when isFailure =>
                HandleDegradedFailure(current, options, now),

            "Degraded" => current with
            {
                CurrentStatus = "Healthy",
                PreviousStatus = "Degraded",
                ConsecutiveFailures = 0,
                ConsecutiveSuccesses = 0,
                LastTransitionAt = now,
                LastDescription = "Service recovered"
            },

            "Unhealthy" when isFailure => current with
            {
                ConsecutiveSuccesses = 0,
                LastDescription = $"Health check returned {checkResult}"
            },

            "Unhealthy" =>
                HandleUnhealthySuccess(current, options, now),

            _ => current
        };
    }

    /// <summary>
    /// Determines whether a state transition occurred between two states.
    /// </summary>
    public static bool HasTransitioned(ServiceHealthState before, ServiceHealthState after)
    {
        return !string.Equals(before.CurrentStatus, after.CurrentStatus, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether a reminder notification should be sent for a persistently unhealthy service.
    /// Returns true when the service has been unhealthy and was last notified longer than the configured interval.
    /// </summary>
    public static bool NeedsReminder(ServiceHealthState state, HealthMonitorOptions options)
    {
        if (!string.Equals(state.CurrentStatus, "Unhealthy", StringComparison.Ordinal))
            return false;

        if (state.LastNotifiedAt is null)
            return false;

        var elapsed = DateTime.UtcNow - state.LastNotifiedAt.Value;
        return elapsed.TotalMinutes > options.ReminderIntervalMinutes;
    }

    private static ServiceHealthState HandleHealthyFailure(
        ServiceHealthState current, HealthStatus checkResult, HealthMonitorOptions options, DateTime now)
    {
        var newFailures = current.ConsecutiveFailures + 1;

        // Hysteresis-in: only transition to Degraded (which fires an alert) once the
        // failure streak reaches DegradedThreshold. Below it the service stays Healthy —
        // no transition, so no alert — and we just accumulate the streak. A single
        // success (below) resets it, so isolated transient blips never emit an alert.
        if (newFailures >= options.DegradedThreshold)
        {
            return current with
            {
                CurrentStatus = "Degraded",
                PreviousStatus = "Healthy",
                ConsecutiveFailures = newFailures,
                ConsecutiveSuccesses = 0,
                LastTransitionAt = now,
                LastDescription = $"Health check returned {checkResult}"
            };
        }

        return current with
        {
            PreviousStatus = "Healthy",
            ConsecutiveFailures = newFailures,
            ConsecutiveSuccesses = 0,
            LastDescription = $"Health check degraded ({newFailures}/{options.DegradedThreshold})"
        };
    }

    private static ServiceHealthState HandleDegradedFailure(
        ServiceHealthState current, HealthMonitorOptions options, DateTime now)
    {
        var newFailures = current.ConsecutiveFailures + 1;

        if (newFailures >= options.UnhealthyThreshold)
        {
            return current with
            {
                CurrentStatus = "Unhealthy",
                PreviousStatus = "Degraded",
                ConsecutiveFailures = newFailures,
                ConsecutiveSuccesses = 0,
                LastTransitionAt = now,
                LastDescription = $"Service exceeded unhealthy threshold ({options.UnhealthyThreshold} consecutive failures)"
            };
        }

        return current with
        {
            ConsecutiveFailures = newFailures,
            LastDescription = $"Health check failed ({newFailures}/{options.UnhealthyThreshold})"
        };
    }

    private static ServiceHealthState HandleUnhealthySuccess(
        ServiceHealthState current, HealthMonitorOptions options, DateTime now)
    {
        var newSuccesses = current.ConsecutiveSuccesses + 1;

        if (newSuccesses >= options.RecoveryThreshold)
        {
            return current with
            {
                CurrentStatus = "Healthy",
                PreviousStatus = "Unhealthy",
                ConsecutiveFailures = 0,
                ConsecutiveSuccesses = newSuccesses,
                LastTransitionAt = now,
                LastDescription = $"Service recovered after {options.RecoveryThreshold} consecutive successes"
            };
        }

        return current with
        {
            ConsecutiveSuccesses = newSuccesses,
            LastDescription = $"Recovery in progress ({newSuccesses}/{options.RecoveryThreshold})"
        };
    }
}

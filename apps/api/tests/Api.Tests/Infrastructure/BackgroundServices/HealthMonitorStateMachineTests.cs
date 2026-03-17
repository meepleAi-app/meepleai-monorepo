using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Configuration;
using Api.Tests.Constants;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// Unit tests for the HealthStateMachine pure-function state machine.
/// Validates hysteresis-based health status transitions with configurable thresholds.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class HealthMonitorStateMachineTests
{
    private static readonly HealthMonitorOptions DefaultOptions = new()
    {
        DegradedThreshold = 1,
        UnhealthyThreshold = 3,
        RecoveryThreshold = 2,
        ReminderIntervalMinutes = 30
    };

    [Fact]
    public void Healthy_service_transitions_to_Degraded_after_one_failure()
    {
        // Arrange
        var state = CreateState("postgres", "Healthy", consecutiveFailures: 0);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        Assert.Equal("Degraded", result.CurrentStatus);
        Assert.Equal("Healthy", result.PreviousStatus);
        Assert.Equal(1, result.ConsecutiveFailures);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Degraded_service_transitions_to_Unhealthy_after_threshold_failures()
    {
        // Arrange — Degraded with 2 consecutive failures (threshold is 3)
        var state = CreateState("redis", "Degraded", consecutiveFailures: 2);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        Assert.Equal("Unhealthy", result.CurrentStatus);
        Assert.Equal("Degraded", result.PreviousStatus);
        Assert.Equal(3, result.ConsecutiveFailures);
    }

    [Fact]
    public void Degraded_stays_Degraded_before_threshold()
    {
        // Arrange — Degraded with 1 consecutive failure (threshold is 3)
        var state = CreateState("qdrant", "Degraded", consecutiveFailures: 1);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        Assert.Equal("Degraded", result.CurrentStatus);
        Assert.Equal(2, result.ConsecutiveFailures);
    }

    [Fact]
    public void Unhealthy_service_recovers_after_threshold_successes()
    {
        // Arrange — Unhealthy with 1 consecutive success (threshold is 2)
        var state = CreateState("postgres", "Unhealthy", consecutiveSuccesses: 1);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        Assert.Equal("Healthy", result.CurrentStatus);
        Assert.Equal("Unhealthy", result.PreviousStatus);
        Assert.Equal(2, result.ConsecutiveSuccesses);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Unhealthy_stays_Unhealthy_before_recovery_threshold()
    {
        // Arrange — Unhealthy with 0 consecutive successes (threshold is 2)
        var state = CreateState("redis", "Unhealthy", consecutiveSuccesses: 0);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        Assert.Equal("Unhealthy", result.CurrentStatus);
        Assert.Equal(1, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Unhealthy_resets_success_counter_on_failure()
    {
        // Arrange — Unhealthy with 1 consecutive success
        var state = CreateState("qdrant", "Unhealthy", consecutiveSuccesses: 1);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        Assert.Equal("Unhealthy", result.CurrentStatus);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Degraded_recovers_to_Healthy_on_success()
    {
        // Arrange
        var state = CreateState("postgres", "Degraded", consecutiveFailures: 2);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        Assert.Equal("Healthy", result.CurrentStatus);
        Assert.Equal("Degraded", result.PreviousStatus);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Healthy_stays_Healthy_on_success()
    {
        // Arrange
        var state = CreateState("postgres", "Healthy");

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        Assert.Equal("Healthy", result.CurrentStatus);
        Assert.Equal("Healthy", result.PreviousStatus);
    }

    [Fact]
    public void Transition_detected_returns_true()
    {
        // Arrange
        var before = CreateState("postgres", "Healthy");
        var after = before with { CurrentStatus = "Degraded", PreviousStatus = "Healthy" };

        // Act
        var result = HealthStateMachine.HasTransitioned(before, after);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void No_transition_returns_false()
    {
        // Arrange
        var before = CreateState("postgres", "Healthy");
        var after = before with { ConsecutiveSuccesses = 1 };

        // Act
        var result = HealthStateMachine.HasTransitioned(before, after);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Reminder_needed_when_unhealthy_past_interval()
    {
        // Arrange — Unhealthy, last notified 35 minutes ago
        var state = CreateState("postgres", "Unhealthy",
            lastNotifiedAt: DateTime.UtcNow.AddMinutes(-35));

        // Act
        var result = HealthStateMachine.NeedsReminder(state, DefaultOptions);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Reminder_not_needed_when_recently_notified()
    {
        // Arrange — Unhealthy, last notified 10 minutes ago
        var state = CreateState("postgres", "Unhealthy",
            lastNotifiedAt: DateTime.UtcNow.AddMinutes(-10));

        // Act
        var result = HealthStateMachine.NeedsReminder(state, DefaultOptions);

        // Assert
        Assert.False(result);
    }

    private static ServiceHealthState CreateState(
        string serviceName,
        string status,
        int consecutiveFailures = 0,
        int consecutiveSuccesses = 0,
        DateTime? lastNotifiedAt = null,
        string? description = null)
    {
        return new ServiceHealthState(
            ServiceName: serviceName,
            CurrentStatus: status,
            PreviousStatus: status,
            ConsecutiveFailures: consecutiveFailures,
            ConsecutiveSuccesses: consecutiveSuccesses,
            LastTransitionAt: DateTime.UtcNow,
            LastNotifiedAt: lastNotifiedAt,
            LastDescription: description);
    }
}

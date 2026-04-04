using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Configuration;
using Api.Tests.Constants;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using FluentAssertions;

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
        result.CurrentStatus.Should().Be("Degraded");
        result.PreviousStatus.Should().Be("Healthy");
        result.ConsecutiveFailures.Should().Be(1);
        result.ConsecutiveSuccesses.Should().Be(0);
    }

    [Fact]
    public void Degraded_service_transitions_to_Unhealthy_after_threshold_failures()
    {
        // Arrange — Degraded with 2 consecutive failures (threshold is 3)
        var state = CreateState("redis", "Degraded", consecutiveFailures: 2);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Unhealthy");
        result.PreviousStatus.Should().Be("Degraded");
        result.ConsecutiveFailures.Should().Be(3);
    }

    [Fact]
    public void Degraded_stays_Degraded_before_threshold()
    {
        // Arrange — Degraded with 1 consecutive failure (threshold is 3)
        var state = CreateState("qdrant", "Degraded", consecutiveFailures: 1);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Degraded");
        result.ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void Unhealthy_service_recovers_after_threshold_successes()
    {
        // Arrange — Unhealthy with 1 consecutive success (threshold is 2)
        var state = CreateState("postgres", "Unhealthy", consecutiveSuccesses: 1);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Healthy");
        result.PreviousStatus.Should().Be("Unhealthy");
        result.ConsecutiveSuccesses.Should().Be(2);
        result.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void Unhealthy_stays_Unhealthy_before_recovery_threshold()
    {
        // Arrange — Unhealthy with 0 consecutive successes (threshold is 2)
        var state = CreateState("redis", "Unhealthy", consecutiveSuccesses: 0);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Unhealthy");
        result.ConsecutiveSuccesses.Should().Be(1);
    }

    [Fact]
    public void Unhealthy_resets_success_counter_on_failure()
    {
        // Arrange — Unhealthy with 1 consecutive success
        var state = CreateState("qdrant", "Unhealthy", consecutiveSuccesses: 1);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Unhealthy");
        result.ConsecutiveSuccesses.Should().Be(0);
    }

    [Fact]
    public void Degraded_recovers_to_Healthy_on_success()
    {
        // Arrange
        var state = CreateState("postgres", "Degraded", consecutiveFailures: 2);

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Healthy");
        result.PreviousStatus.Should().Be("Degraded");
        result.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void Healthy_stays_Healthy_on_success()
    {
        // Arrange
        var state = CreateState("postgres", "Healthy");

        // Act
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, DefaultOptions);

        // Assert
        result.CurrentStatus.Should().Be("Healthy");
        result.PreviousStatus.Should().Be("Healthy");
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
        result.Should().BeTrue();
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
        result.Should().BeFalse();
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
        result.Should().BeTrue();
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
        result.Should().BeFalse();
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

using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for CircuitBreakerState configurable thresholds (Issue #5498).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5498")]
public sealed class CircuitBreakerStateConfigurableThresholdTests
{
    [Fact]
    public void DefaultConstructor_UsesStandardDefaults()
    {
        var state = new CircuitBreakerState();

        state.State.Should().Be(CircuitState.Closed);
        state.AllowsRequests().Should().BeTrue();

        // Should open after 5 failures (default)
        for (var i = 0; i < 5; i++)
            state.RecordFailure();

        state.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void CustomFailureThreshold_OpensAfterConfiguredFailures()
    {
        var state = new CircuitBreakerState(failureThreshold: 3);

        state.RecordFailure();
        state.RecordFailure();
        state.State.Should().Be(CircuitState.Closed);

        state.RecordFailure();
        state.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void CustomSuccessThreshold_ClosesAfterConfiguredSuccesses()
    {
        // Use 1-second open duration so AllowsRequests transitions to HalfOpen after wait
        var state = new CircuitBreakerState(failureThreshold: 1, openDurationSeconds: 1, successThreshold: 2);

        // Open the circuit
        state.RecordFailure();
        state.State.Should().Be(CircuitState.Open);

        // Wait for open duration to expire, then transition to HalfOpen
        Thread.Sleep(1100);
        state.AllowsRequests().Should().BeTrue();
        state.State.Should().Be(CircuitState.HalfOpen);

        // One success isn't enough
        state.RecordSuccess();
        state.State.Should().Be(CircuitState.HalfOpen);

        // Two successes closes it
        state.RecordSuccess();
        state.State.Should().Be(CircuitState.Closed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidFailureThreshold_FallsBackToDefault(int invalidThreshold)
    {
        var state = new CircuitBreakerState(failureThreshold: invalidThreshold);

        // Should use default of 5
        for (var i = 0; i < 4; i++)
            state.RecordFailure();
        state.State.Should().Be(CircuitState.Closed);

        state.RecordFailure();
        state.State.Should().Be(CircuitState.Open);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidSuccessThreshold_FallsBackToDefault(int invalidThreshold)
    {
        var state = new CircuitBreakerState(failureThreshold: 1, openDurationSeconds: 1, successThreshold: invalidThreshold);

        // Open and transition to HalfOpen
        state.RecordFailure();
        Thread.Sleep(1100);
        state.AllowsRequests();
        state.State.Should().Be(CircuitState.HalfOpen);

        // Should use default of 3
        state.RecordSuccess();
        state.RecordSuccess();
        state.State.Should().Be(CircuitState.HalfOpen);

        state.RecordSuccess();
        state.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void GetStatus_ShowsConfiguredSuccessThreshold()
    {
        var state = new CircuitBreakerState(failureThreshold: 1, openDurationSeconds: 1, successThreshold: 5);

        state.RecordFailure();
        Thread.Sleep(1100);
        state.AllowsRequests(); // HalfOpen
        state.RecordSuccess();

        var status = state.GetStatus();
        status.Should().Contain("/5"); // Shows configured threshold
    }

    [Fact]
    public void Reset_ReturnsToClosedRegardlessOfThresholds()
    {
        var state = new CircuitBreakerState(failureThreshold: 2);

        state.RecordFailure();
        state.RecordFailure();
        state.State.Should().Be(CircuitState.Open);

        state.Reset();
        state.State.Should().Be(CircuitState.Closed);
        state.ConsecutiveFailures.Should().Be(0);
    }
}

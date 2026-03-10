using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Xunit;

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

        Assert.Equal(CircuitState.Closed, state.State);
        Assert.True(state.AllowsRequests());

        // Should open after 5 failures (default)
        for (var i = 0; i < 5; i++)
            state.RecordFailure();

        Assert.Equal(CircuitState.Open, state.State);
    }

    [Fact]
    public void CustomFailureThreshold_OpensAfterConfiguredFailures()
    {
        var state = new CircuitBreakerState(failureThreshold: 3);

        state.RecordFailure();
        state.RecordFailure();
        Assert.Equal(CircuitState.Closed, state.State);

        state.RecordFailure();
        Assert.Equal(CircuitState.Open, state.State);
    }

    [Fact]
    public void CustomSuccessThreshold_ClosesAfterConfiguredSuccesses()
    {
        // Use 1-second open duration so AllowsRequests transitions to HalfOpen after wait
        var state = new CircuitBreakerState(failureThreshold: 1, openDurationSeconds: 1, successThreshold: 2);

        // Open the circuit
        state.RecordFailure();
        Assert.Equal(CircuitState.Open, state.State);

        // Wait for open duration to expire, then transition to HalfOpen
        Thread.Sleep(1100);
        Assert.True(state.AllowsRequests());
        Assert.Equal(CircuitState.HalfOpen, state.State);

        // One success isn't enough
        state.RecordSuccess();
        Assert.Equal(CircuitState.HalfOpen, state.State);

        // Two successes closes it
        state.RecordSuccess();
        Assert.Equal(CircuitState.Closed, state.State);
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
        Assert.Equal(CircuitState.Closed, state.State);

        state.RecordFailure();
        Assert.Equal(CircuitState.Open, state.State);
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
        Assert.Equal(CircuitState.HalfOpen, state.State);

        // Should use default of 3
        state.RecordSuccess();
        state.RecordSuccess();
        Assert.Equal(CircuitState.HalfOpen, state.State);

        state.RecordSuccess();
        Assert.Equal(CircuitState.Closed, state.State);
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
        Assert.Contains("/5", status); // Shows configured threshold
    }

    [Fact]
    public void Reset_ReturnsToClosedRegardlessOfThresholds()
    {
        var state = new CircuitBreakerState(failureThreshold: 2);

        state.RecordFailure();
        state.RecordFailure();
        Assert.Equal(CircuitState.Open, state.State);

        state.Reset();
        Assert.Equal(CircuitState.Closed, state.State);
        Assert.Equal(0, state.ConsecutiveFailures);
    }
}

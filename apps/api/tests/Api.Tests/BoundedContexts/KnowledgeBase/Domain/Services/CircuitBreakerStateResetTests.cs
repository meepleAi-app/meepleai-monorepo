using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for CircuitBreakerState.Reset() method (Issue #5476).
/// Verifies admin emergency reset from all possible circuit states.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5476")]
public sealed class CircuitBreakerStateResetTests
{
    [Fact]
    public void Reset_FromClosed_StaysClosed()
    {
        var cb = new CircuitBreakerState();

        cb.Reset();

        cb.State.Should().Be(CircuitState.Closed);
        cb.ConsecutiveFailures.Should().Be(0);
        cb.ConsecutiveSuccesses.Should().Be(0);
        cb.OpenedAt.Should().BeNull();
        cb.LastAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public void Reset_FromOpen_TransitionsToClosed()
    {
        var cb = new CircuitBreakerState();

        // Drive to Open state: 5 failures
        for (var i = 0; i < 5; i++)
            cb.RecordFailure();

        cb.State.Should().Be(CircuitState.Open);

        cb.Reset();

        cb.State.Should().Be(CircuitState.Closed);
        cb.ConsecutiveFailures.Should().Be(0);
        cb.OpenedAt.Should().BeNull();
    }

    [Fact]
    public void Reset_FromOpen_FiresTransitionCallback()
    {
        var cb = new CircuitBreakerState();
        CircuitState? fromState = null;
        CircuitState? toState = null;

        // Drive to Open
        for (var i = 0; i < 5; i++)
            cb.RecordFailure();

        cb.OnStateTransition = (from, to) =>
        {
            fromState = from;
            toState = to;
        };

        cb.Reset();

        fromState.Should().Be(CircuitState.Open);
        toState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void Reset_FromClosed_DoesNotFireTransitionCallback()
    {
        var cb = new CircuitBreakerState();
        var callbackInvoked = false;

        cb.OnStateTransition = (_, _) => callbackInvoked = true;

        cb.Reset();

        // No transition from Closed → Closed
        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsCountersAndOpenedAt()
    {
        var cb = new CircuitBreakerState();

        // Drive to Open (5 failures)
        for (var i = 0; i < 5; i++)
            cb.RecordFailure();

        (cb.ConsecutiveFailures > 0).Should().BeTrue();
        cb.OpenedAt.Should().NotBeNull();

        cb.Reset();

        cb.ConsecutiveFailures.Should().Be(0);
        cb.ConsecutiveSuccesses.Should().Be(0);
        cb.OpenedAt.Should().BeNull();
    }

    [Fact]
    public void Reset_SetsLastAttemptAt()
    {
        var cb = new CircuitBreakerState();
        var before = DateTime.UtcNow;

        cb.Reset();

        cb.LastAttemptAt.Should().NotBeNull();
        (cb.LastAttemptAt >= before).Should().BeTrue();
    }

    [Fact]
    public void Reset_AllowsRequestsAfterReset()
    {
        var cb = new CircuitBreakerState();

        // Drive to Open
        for (var i = 0; i < 5; i++)
            cb.RecordFailure();

        cb.AllowsRequests().Should().BeFalse();

        cb.Reset();

        cb.AllowsRequests().Should().BeTrue();
    }
}

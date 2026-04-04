using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Unit tests for CircuitBreakerStateTracker.
/// Covers state transitions (Closed, Open, HalfOpen), trip count accumulation,
/// and collection operations (GetAllStates, GetState).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class CircuitBreakerStateTrackerTests
{
    private readonly CircuitBreakerStateTracker _sut = new();

    [Fact]
    public void RegisterService_CreatesClosedState()
    {
        // Arrange
        const string serviceName = "TestService";

        // Act
        _sut.RegisterService(serviceName);

        // Assert
        var state = _sut.GetState(serviceName);
        state.Should().NotBeNull();
        state!.ServiceName.Should().Be(serviceName);
        state.State.Should().Be("Closed");
        state.TripCount.Should().Be(0);
    }

    [Fact]
    public void RecordBreak_SetsOpenStateAndIncrementsTripCount()
    {
        // Arrange
        const string serviceName = "TestService";
        const string errorMessage = "Connection refused";
        _sut.RegisterService(serviceName);

        // Act
        _sut.RecordBreak(serviceName, errorMessage);

        // Assert
        var state = _sut.GetState(serviceName);
        state.Should().NotBeNull();
        state!.State.Should().Be("Open");
        state.TripCount.Should().Be(1);
        state.LastError.Should().Be(errorMessage);
        state.LastTrippedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordReset_SetsClosedState()
    {
        // Arrange
        const string serviceName = "TestService";
        _sut.RegisterService(serviceName);
        _sut.RecordBreak(serviceName, "some error");

        // Act
        _sut.RecordReset(serviceName);

        // Assert
        var state = _sut.GetState(serviceName);
        state.Should().NotBeNull();
        state!.State.Should().Be("Closed");
        state.LastResetAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordHalfOpen_SetsHalfOpenState()
    {
        // Arrange
        const string serviceName = "TestService";
        _sut.RegisterService(serviceName);
        _sut.RecordBreak(serviceName, "some error");

        // Act
        _sut.RecordHalfOpen(serviceName);

        // Assert
        var state = _sut.GetState(serviceName);
        state.Should().NotBeNull();
        state!.State.Should().Be("HalfOpen");
    }

    [Fact]
    public void GetAllStates_ReturnsAllRegisteredServices()
    {
        // Arrange
        _sut.RegisterService("ServiceA");
        _sut.RegisterService("ServiceB");

        // Act
        var states = _sut.GetAllStates();

        // Assert
        states.Should().HaveCount(2);
        states.Select(s => s.ServiceName).Should().Contain(new[] { "ServiceA", "ServiceB" });
    }

    [Fact]
    public void GetState_UnknownService_ReturnsNull()
    {
        // Act
        var state = _sut.GetState("NonExistentService");

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void MultipleBreaks_IncrementTripCount()
    {
        // Arrange
        const string serviceName = "TestService";
        const string firstError = "First failure";
        const string secondError = "Second failure";
        _sut.RegisterService(serviceName);

        // Act
        _sut.RecordBreak(serviceName, firstError);
        _sut.RecordReset(serviceName);
        _sut.RecordBreak(serviceName, secondError);

        // Assert
        var state = _sut.GetState(serviceName);
        state.Should().NotBeNull();
        state!.TripCount.Should().Be(2);
        state.LastError.Should().Be(secondError);
    }
}

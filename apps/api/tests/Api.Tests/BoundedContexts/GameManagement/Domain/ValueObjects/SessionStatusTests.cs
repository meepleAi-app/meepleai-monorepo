using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the SessionStatus value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 19
/// </summary>
[Trait("Category", "Unit")]
public sealed class SessionStatusTests
{
    #region Static Instances Tests

    [Fact]
    public void Setup_HasCorrectValue()
    {
        // Assert
        SessionStatus.Setup.Value.Should().Be("Setup");
    }

    [Fact]
    public void InProgress_HasCorrectValue()
    {
        // Assert
        SessionStatus.InProgress.Value.Should().Be("InProgress");
    }

    [Fact]
    public void Paused_HasCorrectValue()
    {
        // Assert
        SessionStatus.Paused.Value.Should().Be("Paused");
    }

    [Fact]
    public void Completed_HasCorrectValue()
    {
        // Assert
        SessionStatus.Completed.Value.Should().Be("Completed");
    }

    [Fact]
    public void Abandoned_HasCorrectValue()
    {
        // Assert
        SessionStatus.Abandoned.Value.Should().Be("Abandoned");
    }

    #endregion

    #region IsActive Tests

    [Fact]
    public void IsActive_ForSetup_ReturnsTrue()
    {
        // Assert
        SessionStatus.Setup.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ForInProgress_ReturnsTrue()
    {
        // Assert
        SessionStatus.InProgress.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ForPaused_ReturnsTrue()
    {
        // Assert
        SessionStatus.Paused.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ForCompleted_ReturnsFalse()
    {
        // Assert
        SessionStatus.Completed.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ForAbandoned_ReturnsFalse()
    {
        // Assert
        SessionStatus.Abandoned.IsActive.Should().BeFalse();
    }

    #endregion

    #region IsFinished Tests

    [Fact]
    public void IsFinished_ForSetup_ReturnsFalse()
    {
        // Assert
        SessionStatus.Setup.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void IsFinished_ForInProgress_ReturnsFalse()
    {
        // Assert
        SessionStatus.InProgress.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void IsFinished_ForPaused_ReturnsFalse()
    {
        // Assert
        SessionStatus.Paused.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void IsFinished_ForCompleted_ReturnsTrue()
    {
        // Assert
        SessionStatus.Completed.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void IsFinished_ForAbandoned_ReturnsTrue()
    {
        // Assert
        SessionStatus.Abandoned.IsFinished.Should().BeTrue();
    }

    #endregion

    #region Mutual Exclusivity Tests

    [Fact]
    public void ActiveAndFinished_AreMutuallyExclusive()
    {
        // Arrange - test all statuses
        var allStatuses = new[]
        {
            SessionStatus.Setup,
            SessionStatus.InProgress,
            SessionStatus.Paused,
            SessionStatus.Completed,
            SessionStatus.Abandoned
        };

        // Assert - no status should be both active and finished
        foreach (var status in allStatuses)
        {
            (status.IsActive && status.IsFinished).Should().BeFalse(
                $"Status {status.Value} should not be both active and finished");
        }
    }

    [Fact]
    public void AllStatuses_AreEitherActiveOrFinished()
    {
        // Arrange - test all statuses
        var allStatuses = new[]
        {
            SessionStatus.Setup,
            SessionStatus.InProgress,
            SessionStatus.Paused,
            SessionStatus.Completed,
            SessionStatus.Abandoned
        };

        // Assert - every status should be either active or finished
        foreach (var status in allStatuses)
        {
            (status.IsActive || status.IsFinished).Should().BeTrue(
                $"Status {status.Value} should be either active or finished");
        }
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameStatus_ReturnsTrue()
    {
        // Arrange
        var status1 = SessionStatus.InProgress;
        var status2 = SessionStatus.InProgress;

        // Assert
        status1.Should().Be(status2);
    }

    [Fact]
    public void Equals_WithDifferentStatus_ReturnsFalse()
    {
        // Arrange
        var status1 = SessionStatus.InProgress;
        var status2 = SessionStatus.Completed;

        // Assert
        status1.Should().NotBe(status2);
    }

    [Fact]
    public void GetHashCode_WithSameStatus_ReturnsSameHash()
    {
        // Arrange
        var status1 = SessionStatus.Setup;
        var status2 = SessionStatus.Setup;

        // Assert
        status1.GetHashCode().Should().Be(status2.GetHashCode());
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        SessionStatus.Setup.ToString().Should().Be("Setup");
        SessionStatus.InProgress.ToString().Should().Be("InProgress");
        SessionStatus.Paused.ToString().Should().Be("Paused");
        SessionStatus.Completed.ToString().Should().Be("Completed");
        SessionStatus.Abandoned.ToString().Should().Be("Abandoned");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        string setup = SessionStatus.Setup;
        string inProgress = SessionStatus.InProgress;
        string paused = SessionStatus.Paused;
        string completed = SessionStatus.Completed;
        string abandoned = SessionStatus.Abandoned;

        // Assert
        setup.Should().Be("Setup");
        inProgress.Should().Be("InProgress");
        paused.Should().Be("Paused");
        completed.Should().Be("Completed");
        abandoned.Should().Be("Abandoned");
    }

    #endregion
}

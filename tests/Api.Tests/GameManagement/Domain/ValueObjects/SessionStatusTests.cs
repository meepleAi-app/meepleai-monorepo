using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.GameManagement.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class SessionStatusTests
{
    #region Static Instance Tests

    [Fact]
    public void Setup_ReturnsSetupStatus()
    {
        // Act
        var status = SessionStatus.Setup;

        // Assert
        status.Value.Should().Be("Setup");
    }

    [Fact]
    public void InProgress_ReturnsInProgressStatus()
    {
        // Act
        var status = SessionStatus.InProgress;

        // Assert
        status.Value.Should().Be("InProgress");
    }

    [Fact]
    public void Paused_ReturnsPausedStatus()
    {
        // Act
        var status = SessionStatus.Paused;

        // Assert
        status.Value.Should().Be("Paused");
    }

    [Fact]
    public void Completed_ReturnsCompletedStatus()
    {
        // Act
        var status = SessionStatus.Completed;

        // Assert
        status.Value.Should().Be("Completed");
    }

    [Fact]
    public void Abandoned_ReturnsAbandonedStatus()
    {
        // Act
        var status = SessionStatus.Abandoned;

        // Assert
        status.Value.Should().Be("Abandoned");
    }

    #endregion

    #region IsActive Tests

    [Fact]
    public void IsActive_ForSetupStatus_ReturnsTrue()
    {
        SessionStatus.Setup.IsActive.Should().BeTrue("Setup should be active");
    }

    [Fact]
    public void IsActive_ForInProgressStatus_ReturnsTrue()
    {
        SessionStatus.InProgress.IsActive.Should().BeTrue("InProgress should be active");
    }

    [Fact]
    public void IsActive_ForPausedStatus_ReturnsTrue()
    {
        SessionStatus.Paused.IsActive.Should().BeTrue("Paused should be active");
    }

    [Fact]
    public void IsActive_ForCompletedStatus_ReturnsFalse()
    {
        SessionStatus.Completed.IsActive.Should().BeFalse("Completed should not be active");
    }

    [Fact]
    public void IsActive_ForAbandonedStatus_ReturnsFalse()
    {
        SessionStatus.Abandoned.IsActive.Should().BeFalse("Abandoned should not be active");
    }

    #endregion

    #region IsFinished Tests

    [Fact]
    public void IsFinished_ForCompletedStatus_ReturnsTrue()
    {
        SessionStatus.Completed.IsFinished.Should().BeTrue("Completed should be finished");
    }

    [Fact]
    public void IsFinished_ForAbandonedStatus_ReturnsTrue()
    {
        SessionStatus.Abandoned.IsFinished.Should().BeTrue("Abandoned should be finished");
    }

    [Fact]
    public void IsFinished_ForSetupStatus_ReturnsFalse()
    {
        SessionStatus.Setup.IsFinished.Should().BeFalse("Setup should not be finished");
    }

    [Fact]
    public void IsFinished_ForInProgressStatus_ReturnsFalse()
    {
        SessionStatus.InProgress.IsFinished.Should().BeFalse("InProgress should not be finished");
    }

    [Fact]
    public void IsFinished_ForPausedStatus_ReturnsFalse()
    {
        SessionStatus.Paused.IsFinished.Should().BeFalse("Paused should not be finished");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsStatusValue()
    {
        // Arrange & Act
        var result = SessionStatus.InProgress.ToString();

        // Assert
        result.Should().Be("InProgress");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        // Arrange
        SessionStatus status = SessionStatus.Completed;

        // Act
        string stringValue = status;

        // Assert
        stringValue.Should().Be("Completed");
    }

    [Fact]
    public void ImplicitConversion_CanBeUsedInStringComparison()
    {
        // Arrange
        SessionStatus status = SessionStatus.Setup;

        // Act & Assert
        (status == "Setup").Should().BeTrue();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameStatusReferences_AreEqual()
    {
        // Arrange
        var status1 = SessionStatus.InProgress;
        var status2 = SessionStatus.InProgress;

        // Assert - Same reference due to static readonly
        status1.Should().BeSameAs(status2);
    }

    [Fact]
    public void Equals_DifferentStatuses_AreNotEqual()
    {
        // Arrange
        var setup = SessionStatus.Setup;
        var completed = SessionStatus.Completed;

        // Assert
        setup.Should().NotBe(completed);
    }

    [Fact]
    public void GetHashCode_SameStatus_ReturnsSameHashCode()
    {
        // Arrange
        var status1 = SessionStatus.Paused;
        var status2 = SessionStatus.Paused;

        // Act & Assert
        status1.GetHashCode().Should().Be(status2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentStatuses_ReturnDifferentHashCodes()
    {
        // Arrange
        var allStatuses = new[]
        {
            SessionStatus.Setup,
            SessionStatus.InProgress,
            SessionStatus.Paused,
            SessionStatus.Completed,
            SessionStatus.Abandoned
        };

        // Act
        var hashCodes = allStatuses.Select(s => s.GetHashCode()).ToList();

        // Assert - All different hash codes
        hashCodes.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region State Coverage Tests

    [Fact]
    public void AllStatuses_AreDistinct()
    {
        // Arrange
        var allStatuses = new[]
        {
            SessionStatus.Setup,
            SessionStatus.InProgress,
            SessionStatus.Paused,
            SessionStatus.Completed,
            SessionStatus.Abandoned
        };

        // Assert
        allStatuses.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllStatuses_HaveNonEmptyValue()
    {
        // Arrange
        var allStatuses = new[]
        {
            SessionStatus.Setup,
            SessionStatus.InProgress,
            SessionStatus.Paused,
            SessionStatus.Completed,
            SessionStatus.Abandoned
        };

        // Assert
        allStatuses.Should().AllSatisfy(s =>
            s.Value.Should().NotBeNullOrWhiteSpace());
    }

    #endregion
}

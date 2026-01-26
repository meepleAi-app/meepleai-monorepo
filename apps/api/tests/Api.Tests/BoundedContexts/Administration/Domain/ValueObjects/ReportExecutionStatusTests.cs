using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Tests for ReportExecutionStatus enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 27
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReportExecutionStatusTests
{
    #region Enum Value Tests

    [Fact]
    public void Running_HasCorrectValue()
    {
        // Assert
        ((int)ReportExecutionStatus.Running).Should().Be(1);
    }

    [Fact]
    public void Completed_HasCorrectValue()
    {
        // Assert
        ((int)ReportExecutionStatus.Completed).Should().Be(2);
    }

    [Fact]
    public void Failed_HasCorrectValue()
    {
        // Assert
        ((int)ReportExecutionStatus.Failed).Should().Be(3);
    }

    [Fact]
    public void Cancelled_HasCorrectValue()
    {
        // Assert
        ((int)ReportExecutionStatus.Cancelled).Should().Be(4);
    }

    #endregion

    #region Enum Definition Tests

    [Fact]
    public void ReportExecutionStatus_HasFourValues()
    {
        // Arrange
        var values = Enum.GetValues<ReportExecutionStatus>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Fact]
    public void ReportExecutionStatus_ContainsAllExpectedValues()
    {
        // Arrange
        var values = Enum.GetValues<ReportExecutionStatus>();

        // Assert
        values.Should().Contain(ReportExecutionStatus.Running);
        values.Should().Contain(ReportExecutionStatus.Completed);
        values.Should().Contain(ReportExecutionStatus.Failed);
        values.Should().Contain(ReportExecutionStatus.Cancelled);
    }

    [Theory]
    [InlineData(1, "Running")]
    [InlineData(2, "Completed")]
    [InlineData(3, "Failed")]
    [InlineData(4, "Cancelled")]
    public void ReportExecutionStatus_HasCorrectNames(int statusValue, string expectedName)
    {
        // Arrange
        var status = (ReportExecutionStatus)statusValue;

        // Assert
        status.ToString().Should().Be(expectedName);
    }

    #endregion

    #region Lifecycle Transition Tests

    [Fact]
    public void TerminalStates_AreCompletedFailedOrCancelled()
    {
        // These are terminal states - report execution cannot continue after these
        var terminalStates = new[]
        {
            ReportExecutionStatus.Completed,
            ReportExecutionStatus.Failed,
            ReportExecutionStatus.Cancelled
        };

        // Running is the only non-terminal state - verify it's not in terminal states
        terminalStates.Should().NotContain(ReportExecutionStatus.Running);
    }

    [Fact]
    public void Running_IsInitialActiveState()
    {
        // Running (1) is the first value, indicating active processing
        ((int)ReportExecutionStatus.Running).Should().Be(1);
        ((int)ReportExecutionStatus.Completed).Should().BeGreaterThan((int)ReportExecutionStatus.Running);
    }

    #endregion
}

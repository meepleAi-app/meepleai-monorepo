using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Domain.Entities;

/// <summary>
/// Tests for the WorkflowErrorLog aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkflowErrorLogTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesInstance()
    {
        // Arrange
        var id = Guid.NewGuid();
        var workflowId = "workflow-123";
        var executionId = "exec-456";
        var errorMessage = "Connection timeout";

        // Act
        var errorLog = new WorkflowErrorLog(id, workflowId, executionId, errorMessage);

        // Assert
        errorLog.Id.Should().Be(id);
        errorLog.WorkflowId.Should().Be(workflowId);
        errorLog.ExecutionId.Should().Be(executionId);
        errorLog.ErrorMessage.Should().Be(errorMessage);
        errorLog.NodeName.Should().BeNull();
        errorLog.StackTrace.Should().BeNull();
        errorLog.RetryCount.Should().Be(0);
        errorLog.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_WithOptionalFields_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var workflowId = "workflow-123";
        var executionId = "exec-456";
        var errorMessage = "Node execution failed";
        var nodeName = "HTTP Request";
        var stackTrace = "at System.Net.Http...";

        // Act
        var errorLog = new WorkflowErrorLog(id, workflowId, executionId, errorMessage, nodeName, stackTrace);

        // Assert
        errorLog.NodeName.Should().Be(nodeName);
        errorLog.StackTrace.Should().Be(stackTrace);
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var errorLog = new WorkflowErrorLog(
            id,
            "  workflow-123  ",
            "  exec-456  ",
            "  Error message  ",
            "  NodeName  ");

        // Assert
        errorLog.WorkflowId.Should().Be("workflow-123");
        errorLog.ExecutionId.Should().Be("exec-456");
        errorLog.ErrorMessage.Should().Be("Error message");
        errorLog.NodeName.Should().Be("NodeName");
    }

    [Fact]
    public void Constructor_RaisesWorkflowErrorLoggedEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var workflowId = "workflow-123";
        var executionId = "exec-456";
        var errorMessage = "Test error";
        var nodeName = "TestNode";
        var stackTrace = "stack trace";

        // Act
        var errorLog = new WorkflowErrorLog(id, workflowId, executionId, errorMessage, nodeName, stackTrace);

        // Assert
        var evt = errorLog.DomainEvents.OfType<WorkflowErrorLoggedEvent>().Single();
        evt.ErrorLogId.Should().Be(id);
        evt.WorkflowId.Should().Be(workflowId);
        evt.ExecutionId.Should().Be(executionId);
        evt.ErrorMessage.Should().Be(errorMessage);
        evt.NodeName.Should().Be(nodeName);
        evt.StackTrace.Should().Be(stackTrace);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyWorkflowId_ThrowsArgumentException(string? workflowId)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var action = () => new WorkflowErrorLog(id, workflowId!, "exec-123", "error");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("WorkflowId cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyExecutionId_ThrowsArgumentException(string? executionId)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var action = () => new WorkflowErrorLog(id, "workflow-123", executionId!, "error");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("ExecutionId cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyErrorMessage_ThrowsArgumentException(string? errorMessage)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var action = () => new WorkflowErrorLog(id, "workflow-123", "exec-456", errorMessage!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("ErrorMessage cannot be empty*");
    }

    #endregion

    #region IncrementRetryCount Tests

    [Fact]
    public void IncrementRetryCount_IncreasesCount()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();
        errorLog.ClearDomainEvents();

        // Act
        errorLog.IncrementRetryCount();

        // Assert
        errorLog.RetryCount.Should().Be(1);
    }

    [Fact]
    public void IncrementRetryCount_CalledMultipleTimes_AccumulatesCount()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();

        // Act
        errorLog.IncrementRetryCount();
        errorLog.IncrementRetryCount();
        errorLog.IncrementRetryCount();

        // Assert
        errorLog.RetryCount.Should().Be(3);
    }

    [Fact]
    public void IncrementRetryCount_RaisesWorkflowRetriedEvent()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();
        errorLog.ClearDomainEvents();

        // Act
        errorLog.IncrementRetryCount();

        // Assert
        var evt = errorLog.DomainEvents.OfType<WorkflowRetriedEvent>().Single();
        evt.ErrorLogId.Should().Be(errorLog.Id);
        evt.WorkflowId.Should().Be(errorLog.WorkflowId);
        evt.ExecutionId.Should().Be(errorLog.ExecutionId);
        evt.RetryCount.Should().Be(1);
    }

    #endregion

    #region ShouldRetry Tests

    [Fact]
    public void ShouldRetry_WithZeroRetries_ReturnsTrue()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();

        // Act & Assert
        errorLog.ShouldRetry().Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_BelowMaxRetries_ReturnsTrue()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();
        errorLog.IncrementRetryCount();
        errorLog.IncrementRetryCount();

        // Act & Assert (default max is 3)
        errorLog.ShouldRetry().Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_AtMaxRetries_ReturnsFalse()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();
        errorLog.IncrementRetryCount();
        errorLog.IncrementRetryCount();
        errorLog.IncrementRetryCount();

        // Act & Assert (default max is 3)
        errorLog.ShouldRetry().Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithCustomMaxRetries_RespectsLimit()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();
        errorLog.IncrementRetryCount();

        // Act & Assert
        errorLog.ShouldRetry(maxRetries: 1).Should().BeFalse();
        errorLog.ShouldRetry(maxRetries: 2).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_AboveMaxRetries_ReturnsFalse()
    {
        // Arrange
        var errorLog = CreateTestErrorLog();
        for (int i = 0; i < 5; i++)
        {
            errorLog.IncrementRetryCount();
        }

        // Act & Assert
        errorLog.ShouldRetry().Should().BeFalse();
        errorLog.ShouldRetry(maxRetries: 10).Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static WorkflowErrorLog CreateTestErrorLog()
    {
        return new WorkflowErrorLog(
            Guid.NewGuid(),
            "test-workflow",
            "test-execution",
            "Test error message");
    }

    #endregion
}

using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Tests for the ReportExecution entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 21
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReportExecutionTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidReportId_CreatesRunningExecution()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act
        var execution = ReportExecution.Create(reportId);

        // Assert
        execution.Id.Should().NotBe(Guid.Empty);
        execution.ReportId.Should().Be(reportId);
        execution.Status.Should().Be(ReportExecutionStatus.Running);
        execution.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        execution.CompletedAt.Should().BeNull();
        execution.ErrorMessage.Should().BeNull();
        execution.OutputPath.Should().BeNull();
        execution.FileSizeBytes.Should().BeNull();
        execution.Duration.Should().BeNull();
        execution.ExecutionMetadata.Should().BeEmpty();
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act
        var execution1 = ReportExecution.Create(reportId);
        var execution2 = ReportExecution.Create(reportId);

        // Assert
        execution1.Id.Should().NotBe(execution2.Id);
    }

    #endregion

    #region Complete Tests

    [Fact]
    public void Complete_SetsStatusToCompleted()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());
        Thread.Sleep(10); // Ensure time passes

        // Act
        var completed = execution.Complete("/reports/output.pdf", 1024);

        // Assert
        completed.Status.Should().Be(ReportExecutionStatus.Completed);
        completed.OutputPath.Should().Be("/reports/output.pdf");
        completed.FileSizeBytes.Should().Be(1024);
        completed.CompletedAt.Should().NotBeNull();
        completed.CompletedAt.Should().BeAfter(execution.StartedAt);
        completed.Duration.Should().NotBeNull();
        completed.Duration!.Value.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Complete_PreservesOriginalProperties()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var execution = ReportExecution.Create(reportId);

        // Act
        var completed = execution.Complete("/output.pdf", 2048);

        // Assert
        completed.Id.Should().Be(execution.Id);
        completed.ReportId.Should().Be(reportId);
        completed.StartedAt.Should().Be(execution.StartedAt);
        completed.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Complete_IsImmutable()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());

        // Act
        var completed = execution.Complete("/output.pdf", 1024);

        // Assert - Original execution unchanged
        execution.Status.Should().Be(ReportExecutionStatus.Running);
        execution.CompletedAt.Should().BeNull();
        execution.OutputPath.Should().BeNull();
    }

    #endregion

    #region Fail Tests

    [Fact]
    public void Fail_SetsStatusToFailed()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());
        Thread.Sleep(10);

        // Act
        var failed = execution.Fail("Database connection timeout");

        // Assert
        failed.Status.Should().Be(ReportExecutionStatus.Failed);
        failed.ErrorMessage.Should().Be("Database connection timeout");
        failed.CompletedAt.Should().NotBeNull();
        failed.Duration.Should().NotBeNull();
    }

    [Fact]
    public void Fail_PreservesOriginalProperties()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var execution = ReportExecution.Create(reportId);

        // Act
        var failed = execution.Fail("Error occurred");

        // Assert
        failed.Id.Should().Be(execution.Id);
        failed.ReportId.Should().Be(reportId);
        failed.StartedAt.Should().Be(execution.StartedAt);
        failed.OutputPath.Should().BeNull();
        failed.FileSizeBytes.Should().BeNull();
    }

    [Fact]
    public void Fail_IsImmutable()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());

        // Act
        var failed = execution.Fail("Error");

        // Assert - Original execution unchanged
        execution.Status.Should().Be(ReportExecutionStatus.Running);
        execution.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region WithMetadata Tests

    [Fact]
    public void WithMetadata_AddsMetadataToExecution()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());

        // Act
        var withMetadata = execution.WithMetadata("rowCount", 1000);

        // Assert
        withMetadata.ExecutionMetadata.Should().ContainKey("rowCount");
        withMetadata.ExecutionMetadata["rowCount"].Should().Be(1000);
    }

    [Fact]
    public void WithMetadata_CanAddMultipleEntries()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());

        // Act
        var withMetadata = execution
            .WithMetadata("rowCount", 1000)
            .WithMetadata("queryTime", "2.5s")
            .WithMetadata("format", "PDF");

        // Assert
        withMetadata.ExecutionMetadata.Should().HaveCount(3);
        withMetadata.ExecutionMetadata["rowCount"].Should().Be(1000);
        withMetadata.ExecutionMetadata["queryTime"].Should().Be("2.5s");
        withMetadata.ExecutionMetadata["format"].Should().Be("PDF");
    }

    [Fact]
    public void WithMetadata_OverwritesExistingKey()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid())
            .WithMetadata("key", "original");

        // Act
        var updated = execution.WithMetadata("key", "updated");

        // Assert
        updated.ExecutionMetadata["key"].Should().Be("updated");
    }

    [Fact]
    public void WithMetadata_IsImmutable()
    {
        // Arrange
        var execution = ReportExecution.Create(Guid.NewGuid());

        // Act
        var withMetadata = execution.WithMetadata("key", "value");

        // Assert - Original unchanged
        execution.ExecutionMetadata.Should().BeEmpty();
        withMetadata.ExecutionMetadata.Should().HaveCount(1);
    }

    [Fact]
    public void WithMetadata_PreservesOtherProperties()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var execution = ReportExecution.Create(reportId);

        // Act
        var withMetadata = execution.WithMetadata("key", "value");

        // Assert
        withMetadata.Id.Should().Be(execution.Id);
        withMetadata.ReportId.Should().Be(reportId);
        withMetadata.Status.Should().Be(ReportExecutionStatus.Running);
        withMetadata.StartedAt.Should().Be(execution.StartedAt);
    }

    #endregion

    #region Complex Workflow Tests

    [Fact]
    public void CompleteWorkflow_CreateToComplete()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act - Simulate full workflow
        var execution = ReportExecution.Create(reportId)
            .WithMetadata("rowCount", 500)
            .WithMetadata("format", "PDF");

        Thread.Sleep(10);

        var completed = execution.Complete("/reports/2024-01-report.pdf", 102400);

        // Assert
        completed.Status.Should().Be(ReportExecutionStatus.Completed);
        completed.ReportId.Should().Be(reportId);
        completed.ExecutionMetadata.Should().HaveCount(2);
        completed.OutputPath.Should().Be("/reports/2024-01-report.pdf");
        completed.FileSizeBytes.Should().Be(102400);
        completed.Duration.Should().NotBeNull();
    }

    [Fact]
    public void FailureWorkflow_CreateToFail()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act
        var execution = ReportExecution.Create(reportId)
            .WithMetadata("attempt", 1);

        Thread.Sleep(10);

        var failed = execution.Fail("Connection refused to database server");

        // Assert
        failed.Status.Should().Be(ReportExecutionStatus.Failed);
        failed.ErrorMessage.Should().Contain("Connection refused");
        failed.ExecutionMetadata["attempt"].Should().Be(1);
        failed.OutputPath.Should().BeNull();
        failed.FileSizeBytes.Should().BeNull();
    }

    #endregion
}
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.WorkflowIntegration;

/// <summary>
/// Integration tests for workflow error logging and retry logic.
/// Week 9: WorkflowIntegration error handling layer (5 tests)
/// Tests: Error logging, retry count tracking, retry limit enforcement, stack trace persistence
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "WorkflowIntegration")]
[Trait("Week", "9")]
public sealed class WorkflowErrorRetryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IWorkflowErrorLogRepository? _repository;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public WorkflowErrorRetryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_week9_workflow_errors_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        _repository = new WorkflowErrorLogRepository(_dbContext, mockEventCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task WorkflowError_Log_ShouldPersistErrorDetails()
    {
        // Arrange
        var error = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_123",
            "exec_456",
            "Connection timeout to external API",
            "HttpRequestNode",
            "at HttpRequestNode.execute() line 42"
        );

        // Act
        await _repository!.AddAsync(error, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.WorkflowId.Should().Be("workflow_123");
        retrieved.ExecutionId.Should().Be("exec_456");
        retrieved.ErrorMessage.Should().Be("Connection timeout to external API");
        retrieved.NodeName.Should().Be("HttpRequestNode");
        retrieved.StackTrace.Should().Contain("line 42");
        retrieved.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task WorkflowError_IncrementRetry_ShouldTrackRetryCount()
    {
        // Arrange
        var error = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_retry_test",
            "exec_retry_001",
            "Temporary database connection error"
        );

        await _repository!.AddAsync(error, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Retry 1
        var tracked = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        tracked!.IncrementRetryCount();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Retry 2
        tracked = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        tracked!.IncrementRetryCount();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var final = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        final.Should().NotBeNull();
        final!.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task WorkflowError_ShouldRetry_ShouldEnforceMaxRetryLimit()
    {
        // Arrange
        var error = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_max_retry",
            "exec_max_retry_001",
            "Persistent API failure"
        );

        await _repository!.AddAsync(error, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Retry until max limit (3)
        for (int i = 0; i < 3; i++)
        {
            var tracked = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
            tracked!.IncrementRetryCount();
            await _repository.UpdateAsync(tracked, TestCancellationToken);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
            _dbContext.ChangeTracker.Clear();
        }

        // Assert
        var final = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        final.Should().NotBeNull();
        final!.RetryCount.Should().Be(3);
        final.ShouldRetry(maxRetries: 3).Should().BeFalse("should not retry after max limit");
    }

    [Fact]
    public async Task WorkflowError_ShouldRetry_ShouldAllowRetryBeforeLimit()
    {
        // Arrange
        var error = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_can_retry",
            "exec_can_retry_001",
            "Transient network error"
        );

        await _repository!.AddAsync(error, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert - Should allow retry when count is 0
        error.ShouldRetry(maxRetries: 3).Should().BeTrue();

        _dbContext.ChangeTracker.Clear();

        // Act - Increment once
        var tracked = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        tracked!.IncrementRetryCount();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Should still allow retry when count is 1
        var updated = await _repository.GetByIdAsync(error.Id, TestCancellationToken);
        updated!.ShouldRetry(maxRetries: 3).Should().BeTrue();
    }

    [Fact]
    public async Task WorkflowError_GetByWorkflowId_ShouldFilterCorrectly()
    {
        // Arrange
        var workflow1Error1 = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_filter_test_1",
            "exec_001",
            "Error 1 in workflow 1"
        );

        var workflow1Error2 = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_filter_test_1",
            "exec_002",
            "Error 2 in workflow 1"
        );

        var workflow2Error1 = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_filter_test_2",
            "exec_003",
            "Error in workflow 2"
        );

        await _repository!.AddAsync(workflow1Error1, TestCancellationToken);
        await _repository.AddAsync(workflow1Error2, TestCancellationToken);
        await _repository.AddAsync(workflow2Error1, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var workflow1Errors = await _repository.FindByWorkflowIdAsync("workflow_filter_test_1", TestCancellationToken);

        // Assert
        workflow1Errors.Should().HaveCount(2);
        workflow1Errors.Should().OnlyContain(e => e.WorkflowId == "workflow_filter_test_1");
    }
}
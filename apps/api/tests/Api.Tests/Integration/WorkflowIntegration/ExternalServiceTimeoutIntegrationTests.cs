using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.WorkflowIntegration;

/// <summary>
/// Integration tests for external service timeout handling in workflows.
/// Week 9: WorkflowIntegration timeout and resilience layer (5 tests)
/// Tests: Timeout detection, failed connection logging, resilience patterns, error categorization
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "WorkflowIntegration")]
[Trait("Week", "9")]
public sealed class ExternalServiceTimeoutIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IN8NConfigurationRepository? _configRepository;
    private IWorkflowErrorLogRepository? _errorRepository;

    private static readonly Guid TestUserId = new("92000000-0000-0000-0000-000000000001");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ExternalServiceTimeoutIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_week9_workflow_timeout_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Seed required User for FK constraints
        await SeedTestUserAsync();

        _configRepository = new N8NConfigurationRepository(_dbContext, mockEventCollector.Object);
        _errorRepository = new WorkflowErrorLogRepository(_dbContext, mockEventCollector.Object);
    }

    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-week9-timeout@meepleai.dev",
            DisplayName = "Test User Week 9 Timeout",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
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
    public async Task ConnectionTest_Timeout_ShouldRecordFailureResult()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Timeout Test N8n",
            new WorkflowUrl("https://slow-n8n.meepleai.dev"),
            "timeout_test_key",
            TestUserId
        );

        await _configRepository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Simulate timeout failure
        var tracked = await _configRepository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.RecordTestResult(false, "Connection timeout after 30 seconds");
        await _configRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var tested = await _configRepository.GetByIdAsync(config.Id, TestCancellationToken);
        tested.Should().NotBeNull();
        tested!.LastTestResult.Should().Contain("timeout");
        tested.LastTestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WorkflowExecution_Timeout_ShouldLogTimeoutError()
    {
        // Arrange & Act
        var timeoutError = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_timeout_001",
            "exec_timeout_001",
            "HttpRequest to external API timed out after 30000ms",
            "ExternalAPINode",
            "at System.Net.Http.HttpClient.SendAsync() timeout: 30000ms"
        );

        await _errorRepository!.AddAsync(timeoutError, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var logged = await _errorRepository.GetByIdAsync(timeoutError.Id, TestCancellationToken);
        logged.Should().NotBeNull();
        logged!.ErrorMessage.Should().Contain("timed out");
        logged.StackTrace.Should().Contain("timeout: 30000ms");
        logged.NodeName.Should().Be("ExternalAPINode");
    }

    [Fact]
    public async Task WorkflowError_MultipleTimeouts_ShouldTrackSeparateInstances()
    {
        // Arrange
        var timeout1 = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_multi_timeout",
            "exec_001",
            "First timeout occurrence",
            "APINode1"
        );

        var timeout2 = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_multi_timeout",
            "exec_002",
            "Second timeout occurrence",
            "APINode2"
        );

        var timeout3 = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_multi_timeout",
            "exec_003",
            "Third timeout occurrence",
            "APINode3"
        );

        // Act
        await _errorRepository!.AddAsync(timeout1, TestCancellationToken);
        await _errorRepository.AddAsync(timeout2, TestCancellationToken);
        await _errorRepository.AddAsync(timeout3, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var workflowErrors = await _errorRepository.FindByWorkflowIdAsync("workflow_multi_timeout", TestCancellationToken);
        workflowErrors.Should().HaveCount(3);
        workflowErrors.Select(e => e.NodeName).Should().Contain(new[] { "APINode1", "APINode2", "APINode3" });
    }

    [Fact]
    public async Task ExternalService_FailedConnection_ShouldDeactivateConfig()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Failed Connection N8n",
            new WorkflowUrl("https://unreachable-n8n.meepleai.dev"),
            "failed_connection_key",
            TestUserId
        );

        await _configRepository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Record failure and deactivate
        var tracked = await _configRepository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.RecordTestResult(false, "Connection refused: Host unreachable");
        tracked.Deactivate();
        await _configRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _configRepository.GetByIdAsync(config.Id, TestCancellationToken);
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
        updated.LastTestResult.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task WorkflowError_TimeoutRetry_ShouldRespectRetryLogic()
    {
        // Arrange
        var timeoutError = new WorkflowErrorLog(
            Guid.NewGuid(),
            "workflow_retry_timeout",
            "exec_retry_timeout_001",
            "Request timeout after 60 seconds"
        );

        await _errorRepository!.AddAsync(timeoutError, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Retry timeout errors (up to max retries)
        for (int i = 0; i < 2; i++)
        {
            _dbContext.ChangeTracker.Clear();
            var tracked = await _errorRepository.GetByIdAsync(timeoutError.Id, TestCancellationToken);

            if (tracked!.ShouldRetry(maxRetries: 3))
            {
                tracked.IncrementRetryCount();
                await _errorRepository.UpdateAsync(tracked, TestCancellationToken);
                await _dbContext.SaveChangesAsync(TestCancellationToken);
            }
        }

        // Assert
        var final = await _errorRepository.GetByIdAsync(timeoutError.Id, TestCancellationToken);
        final.Should().NotBeNull();
        final!.RetryCount.Should().Be(2);
        final.ShouldRetry(maxRetries: 3).Should().BeTrue("should still allow one more retry");
    }
}

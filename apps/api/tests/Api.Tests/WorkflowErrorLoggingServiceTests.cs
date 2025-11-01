using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

#pragma warning disable EXTEXP0018 // HybridCache is experimental

public class WorkflowErrorLoggingServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly ServiceProvider _serviceProvider;

    public WorkflowErrorLoggingServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup in-memory SQLite
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup HybridCache with service provider for in-memory caching
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        _serviceProvider = services.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<HybridCache>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        _serviceProvider.Dispose();
    }
    [Fact]
    public async Task LogErrorAsync_WithValidRequest_LogsSuccessfully()
    {
        // Arrange
        var service = CreateService();

        var request = new LogWorkflowErrorRequest(
            WorkflowId: "test-workflow",
            ExecutionId: "exec-123",
            ErrorMessage: "Test error message",
            NodeName: "HTTP Request",
            RetryCount: 1
        );

        // Act
        await service.LogErrorAsync(request);

        // Assert
        var logged = await _dbContext.WorkflowErrorLogs.FirstOrDefaultAsync();
        logged.Should().NotBeNull();
        logged.WorkflowId.Should().Be("test-workflow");
        logged.ExecutionId.Should().Be("exec-123");
        logged.ErrorMessage.Should().Be("Test error message");
        logged.NodeName.Should().Be("HTTP Request");
        logged.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task LogErrorAsync_SanitizesSensitiveData_InErrorMessage()
    {
        // Arrange
        var service = CreateService();

        var request = new LogWorkflowErrorRequest(
            WorkflowId: "test-workflow",
            ExecutionId: "exec-123",
            ErrorMessage: "Error: API_KEY=sk-1234567890abcdef token=bearer-xyz password='secret123'",
            NodeName: "HTTP Request"
        );

        // Act
        await service.LogErrorAsync(request);

        // Assert
        var logged = await _dbContext.WorkflowErrorLogs.FirstOrDefaultAsync();
        logged.Should().NotBeNull();
        logged.ErrorMessage.Should().Contain("***REDACTED***");
        logged.ErrorMessage.Should().NotContain("sk-1234567890abcdef");
        logged.ErrorMessage.Should().NotContain("bearer-xyz");
        logged.ErrorMessage.Should().NotContain("secret123");
    }

    [Fact]
    public async Task LogErrorAsync_TruncatesLongMessages_Above5000Chars()
    {
        // Arrange
        var service = CreateService();

        var longMessage = new string('A', 6000);
        var request = new LogWorkflowErrorRequest(
            WorkflowId: "test-workflow",
            ExecutionId: "exec-123",
            ErrorMessage: longMessage
        );

        // Act
        await service.LogErrorAsync(request);

        // Assert
        var logged = await _dbContext.WorkflowErrorLogs.FirstOrDefaultAsync();
        logged.Should().NotBeNull();
        logged.ErrorMessage.Length <= 5000 + 20.Should().BeTrue(); // +20 for "... [truncated]"
        Assert.EndsWith("... [truncated]", logged.ErrorMessage);
    }

    [Fact]
    public async Task LogErrorAsync_WithStackTrace_StoresCorrectly()
    {
        // Arrange
        var service = CreateService();

        var request = new LogWorkflowErrorRequest(
            WorkflowId: "test-workflow",
            ExecutionId: "exec-123",
            ErrorMessage: "Error occurred",
            StackTrace: "at SomeFunction() line 42\nat AnotherFunction() line 123"
        );

        // Act
        await service.LogErrorAsync(request);

        // Assert
        var logged = await _dbContext.WorkflowErrorLogs.FirstOrDefaultAsync();
        logged.Should().NotBeNull();
        logged.StackTrace.Should().Contain("at SomeFunction()");
    }

    [Fact]
    public async Task LogErrorAsync_DoesNotThrow_OnDatabaseFailure()
    {
        // Arrange
        await _dbContext.Database.EnsureDeletedAsync(); // Break the database
        var service = CreateService();

        var request = new LogWorkflowErrorRequest(
            WorkflowId: "test-workflow",
            ExecutionId: "exec-123",
            ErrorMessage: "Test error"
        );

        // Act & Assert - Should not throw exception (resilience pattern)
        await service.LogErrorAsync(request);
    }

    [Fact]
    public async Task GetErrorsAsync_WithNoFilters_ReturnsAllErrors()
    {
        // Arrange
        await SeedErrorsAsync(5);
        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams();

        // Act
        var result = await service.GetErrorsAsync(queryParams);

        // Assert
        result.Total.Should().Be(5);
        result.Items.Count.Should().Be(5);
    }

    [Fact]
    public async Task GetErrorsAsync_WithWorkflowIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        await SeedErrorAsync("workflow-A", "exec-1");
        await SeedErrorAsync("workflow-A", "exec-2");
        await SeedErrorAsync("workflow-B", "exec-3");
        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams(WorkflowId: "workflow-A");

        // Act
        var result = await service.GetErrorsAsync(queryParams);

        // Assert
        result.Total.Should().Be(2);
        result.Items.Should().OnlyContain(item => item.WorkflowId == "workflow-A");
    }

    [Fact]
    public async Task GetErrorsAsync_WithDateFilter_ReturnsFilteredResults()
    {
        // Arrange
        var now = DateTime.UtcNow;

        await SeedErrorAsync("workflow-1", "exec-1", now.AddDays(-5));
        await SeedErrorAsync("workflow-1", "exec-2", now.AddDays(-2));
        await SeedErrorAsync("workflow-1", "exec-3", now.AddDays(-1));

        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams(
            FromDate: now.AddDays(-3),
            ToDate: now
        );

        // Act
        var result = await service.GetErrorsAsync(queryParams);

        // Assert
        result.Total.Should().Be(2); // Only last 2 errors
    }

    [Fact]
    public async Task GetErrorsAsync_WithPagination_RespectsLimits()
    {
        // Arrange
        await SeedErrorsAsync(25);
        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams(Page: 1, Limit: 10);

        // Act
        var result = await service.GetErrorsAsync(queryParams);

        // Assert
        result.Total.Should().Be(25);
        result.Items.Count.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetErrorsAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var now = DateTime.UtcNow;

        await SeedErrorAsync("workflow-1", "exec-1", now.AddDays(-3));
        await SeedErrorAsync("workflow-1", "exec-2", now.AddDays(-1));
        await SeedErrorAsync("workflow-1", "exec-3", now.AddDays(-2));

        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams();

        // Act
        var result = await service.GetErrorsAsync(queryParams);

        // Assert
        result.Items.Count.Should().Be(3);
        result.Items[0].CreatedAt > result.Items[1].CreatedAt.Should().BeTrue();
        result.Items[1].CreatedAt > result.Items[2].CreatedAt.Should().BeTrue();
    }

    [Fact]
    public async Task GetErrorsAsync_UsesCaching()
    {
        // Arrange
        await SeedErrorsAsync(3);
        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams();

        // Act
        var result1 = await service.GetErrorsAsync(queryParams);

        // Add more errors after first call
        await SeedErrorsAsync(2);

        var result2 = await service.GetErrorsAsync(queryParams);

        // Assert - Should return cached result (3 items, not 5)
        result1.Total.Should().Be(3);
        result2.Total.Should().Be(3); // Cached
    }

    [Fact]
    public async Task GetErrorByIdAsync_WithValidId_ReturnsError()
    {
        // Arrange
        var errorId = await SeedErrorAsync("workflow-1", "exec-1");
        var service = CreateService();

        // Act
        var result = await service.GetErrorByIdAsync(errorId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(errorId);
        result.WorkflowId.Should().Be("workflow-1");
    }

    [Fact]
    public async Task GetErrorByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetErrorByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetErrorByIdAsync_UsesCaching()
    {
        // Arrange
        var errorId = await SeedErrorAsync("workflow-1", "exec-1");
        var service = CreateService();

        // Act
        var result1 = await service.GetErrorByIdAsync(errorId);

        // Delete the error from database
        var entity = await _dbContext.WorkflowErrorLogs.FindAsync(errorId);
        if (entity != null)
        {
            _dbContext.WorkflowErrorLogs.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }

        var result2 = await service.GetErrorByIdAsync(errorId);

        // Assert - Should return cached result (not null)
        result1.Should().NotBeNull();
        result2.Should().NotBeNull(); // Cached, even though deleted from DB
    }

    [Fact]
    public async Task GetErrorsAsync_WithCombinedFilters_AppliesAllFilters()
    {
        // Arrange
        var now = DateTime.UtcNow;

        await SeedErrorAsync("workflow-A", "exec-1", now.AddDays(-5));
        await SeedErrorAsync("workflow-A", "exec-2", now.AddDays(-2));
        await SeedErrorAsync("workflow-B", "exec-3", now.AddDays(-1));
        await SeedErrorAsync("workflow-A", "exec-4", now.AddDays(-1));

        var service = CreateService();

        var queryParams = new WorkflowErrorsQueryParams(
            WorkflowId: "workflow-A",
            FromDate: now.AddDays(-3),
            ToDate: now,
            Page: 1,
            Limit: 10
        );

        // Act
        var result = await service.GetErrorsAsync(queryParams);

        // Assert
        result.Total.Should().Be(2); // Only workflow-A errors in date range
        result.Items.Should().OnlyContain(item => item.WorkflowId == "workflow-A");
    }

    // Helper methods

    private WorkflowErrorLoggingService CreateService()
    {
        var logger = new Mock<ILogger<WorkflowErrorLoggingService>>().Object;
        return new WorkflowErrorLoggingService(_dbContext, _cache, logger);
    }

    private async Task<Guid> SeedErrorAsync(
        string workflowId,
        string executionId,
        DateTime? createdAt = null)
    {
        var error = new WorkflowErrorLogEntity
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            ExecutionId = executionId,
            ErrorMessage = $"Error in {workflowId}",
            NodeName = "Test Node",
            RetryCount = 0,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        _dbContext.WorkflowErrorLogs.Add(error);
        await _dbContext.SaveChangesAsync();

        return error.Id;
    }

    private async Task SeedErrorsAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await SeedErrorAsync($"workflow-{i}", $"exec-{i}");
        }
    }
}
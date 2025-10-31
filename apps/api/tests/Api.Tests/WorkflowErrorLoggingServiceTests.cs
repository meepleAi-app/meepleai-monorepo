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
        Assert.NotNull(logged);
        Assert.Equal("test-workflow", logged.WorkflowId);
        Assert.Equal("exec-123", logged.ExecutionId);
        Assert.Equal("Test error message", logged.ErrorMessage);
        Assert.Equal("HTTP Request", logged.NodeName);
        Assert.Equal(1, logged.RetryCount);
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
        Assert.NotNull(logged);
        Assert.Contains("***REDACTED***", logged.ErrorMessage);
        Assert.DoesNotContain("sk-1234567890abcdef", logged.ErrorMessage);
        Assert.DoesNotContain("bearer-xyz", logged.ErrorMessage);
        Assert.DoesNotContain("secret123", logged.ErrorMessage);
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
        Assert.NotNull(logged);
        Assert.True(logged.ErrorMessage.Length <= 5000 + 20); // +20 for "... [truncated]"
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
        Assert.NotNull(logged);
        Assert.Contains("at SomeFunction()", logged.StackTrace);
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
        Assert.Equal(5, result.Total);
        Assert.Equal(5, result.Items.Count);
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
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Equal("workflow-A", item.WorkflowId));
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
        Assert.Equal(2, result.Total); // Only last 2 errors
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
        Assert.Equal(25, result.Total);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
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
        Assert.Equal(3, result.Items.Count);
        Assert.True(result.Items[0].CreatedAt > result.Items[1].CreatedAt);
        Assert.True(result.Items[1].CreatedAt > result.Items[2].CreatedAt);
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
        Assert.Equal(3, result1.Total);
        Assert.Equal(3, result2.Total); // Cached
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
        Assert.NotNull(result);
        Assert.Equal(errorId, result.Id);
        Assert.Equal("workflow-1", result.WorkflowId);
    }

    [Fact]
    public async Task GetErrorByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetErrorByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
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
        Assert.NotNull(result1);
        Assert.NotNull(result2); // Cached, even though deleted from DB
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
        Assert.Equal(2, result.Total); // Only workflow-A errors in date range
        Assert.All(result.Items, item => Assert.Equal("workflow-A", item.WorkflowId));
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
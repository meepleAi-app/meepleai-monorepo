using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class AuditServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<AuditService>> _loggerMock;
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        _loggerMock = new Mock<ILogger<AuditService>>();
        _service = new AuditService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task LogAsync_CreatesAuditLog()
    {
        // Arrange
        var tenantId = "test-tenant";
        var userId = "user-123";
        var action = "TestAction";
        var resource = "TestResource";
        var resourceId = "resource-456";

        // Act
        await _service.LogAsync(
            tenantId,
            userId,
            action,
            resource,
            resourceId,
            "Success",
            "Test details");

        // Assert
        var logs = await _dbContext.AuditLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal(tenantId, logs[0].TenantId);
        Assert.Equal(userId, logs[0].UserId);
        Assert.Equal(action, logs[0].Action);
        Assert.Equal(resource, logs[0].Resource);
        Assert.Equal(resourceId, logs[0].ResourceId);
        Assert.Equal("Success", logs[0].Result);
        Assert.Equal("Test details", logs[0].Details);
    }

    [Fact]
    public async Task LogAsync_SetsTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        await _service.LogAsync("tenant", "user", "action", "resource", "id", "Success");

        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.True(log.CreatedAt >= before && log.CreatedAt <= after);
    }

    [Fact]
    public async Task LogAsync_HandlesNullDetails()
    {
        // Act
        await _service.LogAsync("tenant", "user", "action", "resource", "id", "Success", null);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.Null(log.Details);
    }

    [Fact]
    public async Task LogTenantAccessDeniedAsync_CreatesLog()
    {
        // Arrange
        var userTenantId = "tenant-1";
        var requestedTenantId = "tenant-2";
        var userId = "user-123";
        var resource = "Game";

        // Act
        await _service.LogTenantAccessDeniedAsync(
            userTenantId,
            requestedTenantId,
            userId,
            resource);

        // Assert
        var logs = await _dbContext.AuditLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal(userTenantId, logs[0].TenantId);
        Assert.Equal(userId, logs[0].UserId);
        Assert.Equal("ACCESS_DENIED", logs[0].Action);
        Assert.Equal(resource, logs[0].Resource);
        Assert.Equal("Denied", logs[0].Result);
        Assert.Contains(requestedTenantId, logs[0].Details);
    }
}

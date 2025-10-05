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
        var userId = "user-123";
        var action = "TestAction";
        var resource = "TestResource";
        var resourceId = "resource-456";

        // Act
        await _service.LogAsync(
            userId,
            action,
            resource,
            resourceId,
            "Success",
            "Test details");

        // Assert
        var logs = await _dbContext.AuditLogs.ToListAsync();
        Assert.Single(logs);
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
        await _service.LogAsync("user", "action", "resource", "id", "Success");

        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.True(log.CreatedAt >= before && log.CreatedAt <= after);
    }

    [Fact]
    public async Task LogAsync_HandlesNullDetails()
    {
        // Act
        await _service.LogAsync("user", "action", "resource", "id", "Success", null);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.Null(log.Details);
    }

    [Fact]
    public async Task LogAccessDeniedAsync_CreatesLog()
    {
        // Arrange
        var userScope = "scope-1";
        var requiredScope = "scope-2";
        var userId = "user-123";
        var resource = "Game";

        // Act
        await _service.LogAccessDeniedAsync(
            userScope,
            requiredScope,
            userId,
            resource);

        // Assert
        var logs = await _dbContext.AuditLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal(userId, logs[0].UserId);
        Assert.Equal("ACCESS_DENIED", logs[0].Action);
        Assert.Equal(resource, logs[0].Resource);
        Assert.Equal("Denied", logs[0].Result);
        Assert.Contains(requiredScope, logs[0].Details);
    }

    [Fact]
    public async Task LogAsync_WithIpAddressAndUserAgent_SavesMetadata()
    {
        // Arrange
        var ipAddress = "192.168.1.1";
        var userAgent = "Mozilla/5.0";

        // Act
        await _service.LogAsync(
            "user",
            "action",
            "resource",
            "id",
            "Success",
            "details",
            ipAddress,
            userAgent);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.Equal(ipAddress, log.IpAddress);
        Assert.Equal(userAgent, log.UserAgent);
    }

    [Fact]
    public async Task LogAccessDeniedAsync_WithIpAddressAndUserAgent_SavesMetadata()
    {
        // Arrange
        var ipAddress = "10.0.0.1";
        var userAgent = "Chrome/91.0";

        // Act
        await _service.LogAccessDeniedAsync(
            "scope-1",
            "scope-2",
            "user-123",
            "Game",
            "game-456",
            ipAddress,
            userAgent);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.Equal(ipAddress, log.IpAddress);
        Assert.Equal(userAgent, log.UserAgent);
        Assert.Equal("game-456", log.ResourceId);
    }

    [Fact]
    public async Task LogAsync_HandlesNullUserId()
    {
        // Act
        await _service.LogAsync(null, "action", "resource", "id", "Success");

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        Assert.Null(log.UserId);
    }
}

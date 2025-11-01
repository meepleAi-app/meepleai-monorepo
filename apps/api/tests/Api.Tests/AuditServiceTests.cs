using System;
using Api.Infrastructure;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

public class AuditServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<AuditService>> _loggerMock;
    private readonly AuditService _service;

    public AuditServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _loggerMock = new Mock<ILogger<AuditService>>();
        _service = new AuditService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task LogAsync_CreatesAuditLogWithGlobalFields()
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
        logs.Should().ContainSingle();
        var log = logs[0];
        string.IsNullOrWhiteSpace(log.Id).Should().BeFalse();
        log.UserId.Should().Be(userId);
        log.Action.Should().Be(action);
        log.Resource.Should().Be(resource);
        log.ResourceId.Should().Be(resourceId);
        log.Result.Should().Be("Success");
        log.Details.Should().Be("Test details");
        log.IpAddress.Should().BeNull();
        log.UserAgent.Should().BeNull();
        log.CreatedAt <= DateTime.UtcNow.Should().BeTrue();
    }

    [Fact]
    public async Task LogAsync_SetsTimestampWithinExpectedRange()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        await _service.LogAsync("user", "action", "resource", "id", "Success");

        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        log.CreatedAt >= before && log.CreatedAt <= after.Should().BeTrue();
    }

    [Fact]
    public async Task LogAsync_HandlesNullDetails()
    {
        // Act
        await _service.LogAsync("user", "action", "resource", "id", "Success", null);

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        log.Details.Should().BeNull();
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
        logs.Should().ContainSingle();
        logs[0].UserId.Should().Be(userId);
        logs[0].Action.Should().Be("ACCESS_DENIED");
        logs[0].Resource.Should().Be(resource);
        logs[0].Result.Should().Be("Denied");
        logs[0].Details.Should().Contain(requiredScope);
        logs[0].Details.Should().Contain(userScope);
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
        log.IpAddress.Should().Be(ipAddress);
        log.UserAgent.Should().Be(userAgent);
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
        log.IpAddress.Should().Be(ipAddress);
        log.UserAgent.Should().Be(userAgent);
        log.ResourceId.Should().Be("game-456");
    }

    [Fact]
    public async Task LogAsync_HandlesNullUserId()
    {
        // Act
        await _service.LogAsync(null, "action", "resource", "id", "Success");

        // Assert
        var log = await _dbContext.AuditLogs.FirstAsync();
        log.UserId.Should().BeNull();
    }
}

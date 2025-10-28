using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// AUTH-07: Unit tests for TempSessionService
/// Tests secure temp session creation, validation, single-use, and expiration
/// </summary>
public class TempSessionServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TempSessionService _tempSessionService;
    private readonly Mock<ILogger<TempSessionService>> _loggerMock;

    public TempSessionServiceTests()
    {
        // Setup SQLite in-memory database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        // Setup temp session service
        _loggerMock = new Mock<ILogger<TempSessionService>>();
        _tempSessionService = new TempSessionService(_dbContext, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateTempSessionAsync_ShouldGenerateSecureToken()
    {
        // Arrange
        var userId = "test-user-1";
        var ipAddress = "192.168.1.1";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var token = await _tempSessionService.CreateTempSessionAsync(userId, ipAddress);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Verify token is stored hashed (not plaintext)
        var storedSession = await _dbContext.TempSessions.FirstOrDefaultAsync(ts => ts.UserId == userId);
        Assert.NotNull(storedSession);
        Assert.NotEqual(token, storedSession.TokenHash); // Should be hashed
        Assert.Equal(ipAddress, storedSession.IpAddress);
        Assert.False(storedSession.IsUsed);
        Assert.True(storedSession.ExpiresAt > DateTime.UtcNow);
        Assert.True(storedSession.ExpiresAt <= DateTime.UtcNow.AddMinutes(6)); // ~5 min TTL
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_WithValidToken_ShouldReturnUserId()
    {
        // Arrange
        var userId = "test-user-2";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test2@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Act
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);

        // Assert
        Assert.Equal(userId, result);

        // Verify session is marked as used
        var session = await _dbContext.TempSessions.FirstOrDefaultAsync(ts => ts.UserId == userId);
        Assert.True(session?.IsUsed);
        Assert.NotNull(session?.UsedAt);
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_SingleUseEnforcement()
    {
        // Arrange
        var userId = "test-user-3";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test3@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Act - First use should succeed
        var result1 = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);
        Assert.Equal(userId, result1);

        // Act - Second use should fail (single-use enforcement)
        var result2 = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);
        Assert.Null(result2);
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_ExpiredSession_ShouldFail()
    {
        // Arrange
        var userId = "test-user-4";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test4@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Create expired session manually
        var expiredSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "expired-hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 min ago
            IsUsed = false
        };
        _dbContext.TempSessions.Add(expiredSession);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync("dummy-token");

        // Assert
        Assert.Null(result); // Should fail for expired session
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_ShouldRemoveOldSessions()
    {
        // Arrange
        var userId = "test-user-5";
        var user = new UserEntity
        {
            Id = userId,
            Email = "test5@example.com",
            PasswordHash = "dummy",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);

        // Create expired session
        var expiredSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "expired-hash",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            IsUsed = false
        };

        // Create used session (>1 hour old)
        var oldUsedSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "old-used-hash",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            IsUsed = true,
            UsedAt = DateTime.UtcNow.AddHours(-2)
        };

        // Create recent session (should NOT be deleted)
        var recentSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "recent-hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        _dbContext.TempSessions.AddRange(expiredSession, oldUsedSession, recentSession);
        await _dbContext.SaveChangesAsync();

        // Act
        await _tempSessionService.CleanupExpiredSessionsAsync();

        // Assert
        var remaining = await _dbContext.TempSessions.ToListAsync();
        Assert.Single(remaining); // Only recent session should remain
        Assert.Equal("recent-hash", remaining[0].TokenHash);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}

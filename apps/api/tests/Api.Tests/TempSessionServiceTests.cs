using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Helpers;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// AUTH-07: Unit tests for TempSessionService
/// Tests secure temp session creation, validation, single-use, and expiration
/// </summary>
public class TempSessionServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly MeepleAiDbContext _dbContext;
    private readonly TempSessionService _tempSessionService;
    private readonly Mock<ILogger<TempSessionService>> _loggerMock;
    private readonly TestTimeProvider _timeProvider;

    public TempSessionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup SQLite in-memory database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        // Setup time provider
        _timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1);

        // Setup temp session service
        _loggerMock = new Mock<ILogger<TempSessionService>>();
        _tempSessionService = new TempSessionService(_dbContext, _loggerMock.Object, _timeProvider);
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
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var token = await _tempSessionService.CreateTempSessionAsync(userId, ipAddress);

        // Assert
        token.Should().NotBeNull();
        token.Should().NotBeEmpty();

        // Verify token is stored hashed (not plaintext)
        var storedSession = await _dbContext.TempSessions.FirstOrDefaultAsync(ts => ts.UserId == userId);
        storedSession.Should().NotBeNull();
        storedSession.TokenHash.Should().NotBe(token); // Should be hashed
        storedSession.IpAddress.Should().Be(ipAddress);
        storedSession.IsUsed.Should().BeFalse();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        storedSession.ExpiresAt > now.Should().BeTrue();
        storedSession.ExpiresAt <= now.AddMinutes(6).Should().BeTrue(); // ~5 min TTL
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
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Act
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);

        // Assert
        result.Should().Be(userId);

        // Verify session is marked as used
        var session = await _dbContext.TempSessions.FirstOrDefaultAsync(ts => ts.UserId == userId);
        session?.IsUsed.Should().BeTrue();
        session?.UsedAt.Should().NotBeNull();
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
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Act - First use should succeed
        var result1 = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);
        result1.Should().Be(userId);

        // Act - Second use should fail (single-use enforcement)
        var result2 = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);
        result2.Should().BeNull();
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
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Create expired session manually
        var now = _timeProvider.GetUtcNow();
        var expiredSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "expired-hash",
            CreatedAt = now.AddMinutes(-10).UtcDateTime,
            ExpiresAt = now.AddMinutes(-5).UtcDateTime, // Expired 5 min ago
            IsUsed = false
        };
        _dbContext.TempSessions.Add(expiredSession);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync("dummy-token");

        // Assert
        result.Should().BeNull(); // Should fail for expired session
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_ShouldRemoveOldSessions()
    {
        // Arrange
        var userId = "test-user-5";
        var now = _timeProvider.GetUtcNow();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test5@example.com",
            PasswordHash = "dummy",
            CreatedAt = now.UtcDateTime
        };
        _dbContext.Users.Add(user);

        // Create expired session
        var expiredSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "expired-hash",
            CreatedAt = now.AddHours(-2).UtcDateTime,
            ExpiresAt = now.AddHours(-1).UtcDateTime,
            IsUsed = false
        };

        // Create used session (>1 hour old)
        var oldUsedSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "old-used-hash",
            CreatedAt = now.AddHours(-3).UtcDateTime,
            ExpiresAt = now.AddHours(-2).UtcDateTime,
            IsUsed = true,
            UsedAt = now.AddHours(-2).UtcDateTime
        };

        // Create recent session (should NOT be deleted)
        var recentSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "recent-hash",
            CreatedAt = now.UtcDateTime,
            ExpiresAt = now.AddMinutes(5).UtcDateTime,
            IsUsed = false
        };

        _dbContext.TempSessions.AddRange(expiredSession, oldUsedSession, recentSession);
        await _dbContext.SaveChangesAsync();

        // Act
        await _tempSessionService.CleanupExpiredSessionsAsync();

        // Assert
        var remaining = await _dbContext.TempSessions.ToListAsync();
        remaining.Should().ContainSingle(); // Only recent session should remain
        remaining[0].TokenHash.Should().Be("recent-hash");
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}

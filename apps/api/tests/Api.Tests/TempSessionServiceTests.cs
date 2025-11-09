using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Helpers;
using Microsoft.Extensions.Time.Testing;
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
    private readonly FakeTimeProvider _timeProvider;

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
        (storedSession.ExpiresAt > now).Should().BeTrue();
        (storedSession.ExpiresAt <= now.AddMinutes(6)).Should().BeTrue(); // ~5 min TTL
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

    #region TEST-574 Security Hardening Tests (P0)

    [Fact]
    public async Task CreateTempSessionAsync_GeneratesUniqueTokens()
    {
        // Arrange
        var userId = "test-user-entropy";
        var user = new UserEntity
        {
            Id = userId,
            Email = "entropy@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act - Generate 20 tokens
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => _tempSessionService.CreateTempSessionAsync(userId))
            .ToArray();
        var tokens = await Task.WhenAll(tasks);

        // Assert - All tokens unique (cryptographic entropy)
        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().AllSatisfy(t => t.Length.Should().BeGreaterThan(32));
    }

    [Fact]
    public async Task CreateTempSessionAsync_TokenHashedInDatabase()
    {
        // Arrange
        var userId = "test-user-hash";
        var user = new UserEntity
        {
            Id = userId,
            Email = "hash@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Assert - Token NOT stored in plaintext
        var session = await _dbContext.TempSessions.FirstAsync(ts => ts.UserId == userId);
        session.TokenHash.Should().NotBe(token);
        session.TokenHash.Length.Should().Be(44); // SHA-256 Base64 = 44 chars
    }

    [Fact]
    public async Task CreateTempSessionAsync_SetsCorrectExpiration()
    {
        // Arrange
        var userId = "test-user-expiry";
        var user = new UserEntity
        {
            Id = userId,
            Email = "expiry@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Act
        await _tempSessionService.CreateTempSessionAsync(userId);

        // Assert - 5 min TTL
        var session = await _dbContext.TempSessions.FirstAsync(ts => ts.UserId == userId);
        var expectedExpiry = now.AddMinutes(5);
        session.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateTempSessionAsync_StoresIpAddress()
    {
        // Arrange
        var userId = "test-user-ip";
        var ipAddress = "203.0.113.42";
        var user = new UserEntity
        {
            Id = userId,
            Email = "ip@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        await _tempSessionService.CreateTempSessionAsync(userId, ipAddress);

        // Assert
        var session = await _dbContext.TempSessions.FirstAsync(ts => ts.UserId == userId);
        session.IpAddress.Should().Be(ipAddress);
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_WithCorruptedToken_ReturnsNull()
    {
        // Arrange
        var userId = "test-user-corrupt";
        var user = new UserEntity
        {
            Id = userId,
            Email = "corrupt@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _tempSessionService.CreateTempSessionAsync(userId);

        // Act - Corrupted/invalid token
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync("corrupted-token-invalid");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_ConcurrentValidation_OnlyOneSucceeds()
    {
        // Arrange
        var userId = "test-user-concurrent-validate";
        var user = new UserEntity
        {
            Id = userId,
            Email = "concurrent@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Act - 10 concurrent validation attempts
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _tempSessionService.ValidateAndConsumeTempSessionAsync(token))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert - Only 1 succeeds (single-use enforcement via transaction)
        results.Count(r => r == userId).Should().Be(1);
        results.Count(r => r == null).Should().Be(9);
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_ExactlyAtExpirationTime_Fails()
    {
        // Arrange
        var userId = "test-user-boundary-exact";
        var user = new UserEntity
        {
            Id = userId,
            Email = "boundary@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var now = _timeProvider.GetUtcNow();
        var session = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "test-hash",
            CreatedAt = now.UtcDateTime,
            ExpiresAt = now.AddMinutes(5).UtcDateTime,
            IsUsed = false
        };
        _dbContext.TempSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Advance time to EXACTLY expiration
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync("dummy");

        // Assert - Should fail at exact boundary
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeTempSessionAsync_OneSecondBeforeExpiration_Succeeds()
    {
        // Arrange
        var userId = "test-user-boundary-before";
        var user = new UserEntity
        {
            Id = userId,
            Email = "before@example.com",
            PasswordHash = "dummy",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = await _tempSessionService.CreateTempSessionAsync(userId);

        // Advance time to 1 second before expiration
        _timeProvider.Advance(TimeSpan.FromMinutes(5).Subtract(TimeSpan.FromSeconds(1)));

        // Act
        var result = await _tempSessionService.ValidateAndConsumeTempSessionAsync(token);

        // Assert - Should succeed just before expiry
        result.Should().Be(userId);
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_KeepsRecentUsedSessions()
    {
        // Arrange
        var userId = "test-user-audit";
        var now = _timeProvider.GetUtcNow();
        var user = new UserEntity
        {
            Id = userId,
            Email = "audit@example.com",
            PasswordHash = "dummy",
            CreatedAt = now.UtcDateTime
        };
        _dbContext.Users.Add(user);

        // Recent used session (<1 hour old BUT not expired) - kept for audit
        var recentUsed = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "recent-used",
            CreatedAt = now.AddMinutes(-30).UtcDateTime,
            ExpiresAt = now.AddMinutes(5).UtcDateTime, // NOT expired yet
            IsUsed = true,
            UsedAt = now.AddMinutes(-25).UtcDateTime
        };
        _dbContext.TempSessions.Add(recentUsed);
        await _dbContext.SaveChangesAsync();

        // Act
        await _tempSessionService.CleanupExpiredSessionsAsync();

        // Assert - Recent used sessions kept for audit trail (not expired, used <1h ago)
        var remaining = await _dbContext.TempSessions.ToListAsync();
        remaining.Should().ContainSingle();
        remaining[0].TokenHash.Should().Be("recent-used");
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_DeletesOldUsedSessions()
    {
        // Arrange
        var userId = "test-user-cleanup";
        var now = _timeProvider.GetUtcNow();
        var user = new UserEntity
        {
            Id = userId,
            Email = "cleanup@example.com",
            PasswordHash = "dummy",
            CreatedAt = now.UtcDateTime
        };
        _dbContext.Users.Add(user);

        // Old used session (>1 hour) - should be deleted
        var oldUsed = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "old-used",
            CreatedAt = now.AddHours(-3).UtcDateTime,
            ExpiresAt = now.AddHours(-2).UtcDateTime,
            IsUsed = true,
            UsedAt = now.AddHours(-2).UtcDateTime
        };
        _dbContext.TempSessions.Add(oldUsed);
        await _dbContext.SaveChangesAsync();

        // Act
        await _tempSessionService.CleanupExpiredSessionsAsync();

        // Assert
        var remaining = await _dbContext.TempSessions.ToListAsync();
        remaining.Should().BeEmpty();
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
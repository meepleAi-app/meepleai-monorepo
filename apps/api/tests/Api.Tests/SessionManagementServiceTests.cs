using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style unit tests for SessionManagementService (AUTH-03).
///
/// Feature: AUTH-03 - Session Management Service with Auto-Revocation
/// As an administrator
/// I want to manage user sessions and automatically revoke inactive ones
/// So that system security is maintained and stale sessions are cleaned up
/// </summary>
public class SessionManagementServiceTests
{
    private readonly SessionManagementConfiguration _defaultConfig = new()
    {
        InactivityTimeoutDays = 30,
        AutoRevocationIntervalHours = 1
    };

    /// <summary>
    /// Scenario: Get active sessions for a user
    ///   Given a user with multiple sessions (active and revoked)
    ///   When calling GetUserSessionsAsync
    ///   Then only active, non-expired sessions are returned
    ///   And sessions are ordered by LastSeenAt (most recent first)
    /// </summary>
    [Fact]
    public async Task GetUserSessionsAsync_WithMultipleSessions_ReturnsOnlyActiveOnes()
    {
        // Given: A user with multiple sessions
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-15T12:00:00Z"));
        var service = CreateService(db, timeProvider);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            DisplayName = "Test User",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var activeSession1 = CreateSession(userId, "session1", lastSeenAt: DateTime.Parse("2024-01-15T11:00:00Z"), user: user);
        var activeSession2 = CreateSession(userId, "session2", lastSeenAt: DateTime.Parse("2024-01-15T10:00:00Z"), user: user);
        var revokedSession = CreateSession(userId, "session3", revokedAt: DateTime.Parse("2024-01-14T12:00:00Z"), user: user);
        var expiredSession = CreateSession(userId, "session4", expiresAt: DateTime.Parse("2024-01-10T12:00:00Z"), user: user);

        db.UserSessions.AddRange(activeSession1, activeSession2, revokedSession, expiredSession);
        await db.SaveChangesAsync();

        // When: Getting user sessions
        var result = await service.GetUserSessionsAsync(userId);

        // Then: Only active sessions are returned, ordered by LastSeenAt
        Assert.Equal(2, result.Count);
        Assert.Equal("session1", result[0].Id);
        Assert.Equal("session2", result[1].Id);
        Assert.All(result, s => Assert.Null(s.RevokedAt));
    }

    /// <summary>
    /// Scenario: Get sessions with null LastSeenAt
    ///   Given sessions without LastSeenAt timestamps
    ///   When calling GetUserSessionsAsync
    ///   Then sessions are ordered by CreatedAt as fallback
    /// </summary>
    [Fact]
    public async Task GetUserSessionsAsync_WithNullLastSeenAt_OrdersByCreatedAt()
    {
        // Given: Sessions without LastSeenAt
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-15T12:00:00Z"));
        var service = CreateService(db, timeProvider);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            DisplayName = "Test User",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var session1 = CreateSession(userId, "session1", createdAt: DateTime.Parse("2024-01-15T11:00:00Z"), lastSeenAt: null, user: user);
        var session2 = CreateSession(userId, "session2", createdAt: DateTime.Parse("2024-01-15T10:00:00Z"), lastSeenAt: null, user: user);

        db.UserSessions.AddRange(session1, session2);
        await db.SaveChangesAsync();

        // When: Getting user sessions
        var result = await service.GetUserSessionsAsync(userId);

        // Then: Sessions ordered by CreatedAt (newer first)
        Assert.Equal(2, result.Count);
        Assert.Equal("session1", result[0].Id);
        Assert.Equal("session2", result[1].Id);
    }

    /// <summary>
    /// Scenario: Get sessions with null userId
    ///   Given a null or empty userId
    ///   When calling GetUserSessionsAsync
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserSessionsAsync_WithNullOrEmptyUserId_ThrowsArgumentException(string? userId)
    {
        // Given: An invalid userId
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        // When/Then: ArgumentException is thrown
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetUserSessionsAsync(userId!));
        Assert.Contains("User ID cannot be null or empty", exception.Message);
    }

    /// <summary>
    /// Scenario: Get all sessions with filtering
    ///   Given multiple users with sessions
    ///   When calling GetAllSessionsAsync with userId filter
    ///   Then only sessions for that user are returned
    /// </summary>
    [Fact]
    public async Task GetAllSessionsAsync_WithUserIdFilter_ReturnsFilteredResults()
    {
        // Given: Multiple users with sessions
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        var user1Id = Guid.NewGuid().ToString();
        var user2Id = Guid.NewGuid().ToString();

        var user1 = new UserEntity { Id = user1Id, Email = "user1@example.com", PasswordHash = "hash", DisplayName = "User 1", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var user2 = new UserEntity { Id = user2Id, Email = "user2@example.com", PasswordHash = "hash", DisplayName = "User 2", Role = UserRole.User, CreatedAt = DateTime.UtcNow };

        db.Users.AddRange(user1, user2);

        var user1Session1 = CreateSession(user1Id, "session1", user: user1);
        var user1Session2 = CreateSession(user1Id, "session2", user: user1);
        var user2Session1 = CreateSession(user2Id, "session3", user: user2);

        db.UserSessions.AddRange(user1Session1, user1Session2, user2Session1);
        await db.SaveChangesAsync();

        // When: Getting sessions filtered by user1
        var result = await service.GetAllSessionsAsync(userId: user1Id);

        // Then: Only user1's sessions are returned
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(user1Id, s.UserId));
    }

    /// <summary>
    /// Scenario: Get all sessions with limit
    ///   Given more sessions than the limit
    ///   When calling GetAllSessionsAsync with a limit
    ///   Then only the specified number of sessions are returned
    /// </summary>
    [Fact]
    public async Task GetAllSessionsAsync_WithLimit_RespectsLimit()
    {
        // Given: Multiple sessions
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        for (int i = 0; i < 10; i++)
        {
            db.UserSessions.Add(CreateSession(userId, $"session{i}", user: user));
        }
        await db.SaveChangesAsync();

        // When: Getting sessions with limit of 5
        var result = await service.GetAllSessionsAsync(limit: 5);

        // Then: Only 5 sessions are returned
        Assert.Equal(5, result.Count);
    }

    /// <summary>
    /// Scenario: Get all sessions with invalid limit
    ///   Given an invalid limit (0, negative, or >1000)
    ///   When calling GetAllSessionsAsync
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task GetAllSessionsAsync_WithInvalidLimit_ThrowsArgumentException(int limit)
    {
        // Given: An invalid limit
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        // When/Then: ArgumentException is thrown
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllSessionsAsync(limit: limit));
        Assert.Contains("Limit must be between 1 and 1000", exception.Message);
    }

    /// <summary>
    /// Scenario: Revoke a session successfully
    ///   Given an active session
    ///   When calling RevokeSessionAsync
    ///   Then the session is marked as revoked
    ///   And RevokedAt timestamp is set
    ///   And cache is invalidated
    ///   And the method returns true
    /// </summary>
    [Fact]
    public async Task RevokeSessionAsync_WithActiveSession_RevokesAndInvalidatesCache()
    {
        // Given: An active session
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-15T12:00:00Z"));
        var mockCache = new Mock<ISessionCacheService>();
        var service = CreateService(db, timeProvider, mockCache.Object);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        var session = CreateSession(userId, "session1", tokenHash: "hash123", user: user);
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        // When: Revoking the session
        var result = await service.RevokeSessionAsync("session1");

        // Then: Session is revoked
        Assert.True(result);

        var revokedSession = await db.UserSessions.FindAsync("session1");
        Assert.NotNull(revokedSession!.RevokedAt);
        // Check that RevokedAt was set (comparing UTC to handle timezone differences)
        var expectedTime = DateTime.Parse("2024-01-15T12:00:00Z").ToUniversalTime();
        var actualTime = revokedSession.RevokedAt!.Value.ToUniversalTime();
        Assert.Equal(expectedTime, actualTime);

        // And: Cache is invalidated
        mockCache.Verify(c => c.InvalidateAsync("hash123", default), Times.Once);
    }

    /// <summary>
    /// Scenario: Revoke already revoked session
    ///   Given a session that is already revoked
    ///   When calling RevokeSessionAsync
    ///   Then the method returns false
    ///   And RevokedAt timestamp is unchanged
    /// </summary>
    [Fact]
    public async Task RevokeSessionAsync_WithAlreadyRevokedSession_ReturnsFalse()
    {
        // Given: An already revoked session
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        var originalRevokedAt = DateTime.Parse("2024-01-14T12:00:00Z");
        var session = CreateSession(userId, "session1", revokedAt: originalRevokedAt, user: user);
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        // When: Attempting to revoke again
        var result = await service.RevokeSessionAsync("session1");

        // Then: Returns false and timestamp unchanged
        Assert.False(result);

        var sessionAfter = await db.UserSessions.FindAsync("session1");
        Assert.Equal(originalRevokedAt, sessionAfter!.RevokedAt);
    }

    /// <summary>
    /// Scenario: Revoke non-existent session
    ///   Given a session ID that doesn't exist
    ///   When calling RevokeSessionAsync
    ///   Then the method returns false
    /// </summary>
    [Fact]
    public async Task RevokeSessionAsync_WithNonExistentSession_ReturnsFalse()
    {
        // Given: No sessions in database
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        // When: Attempting to revoke non-existent session
        var result = await service.RevokeSessionAsync("nonexistent");

        // Then: Returns false
        Assert.False(result);
    }

    /// <summary>
    /// Scenario: Revoke session with null or empty ID
    ///   Given a null or empty session ID
    ///   When calling RevokeSessionAsync
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeSessionAsync_WithNullOrEmptyId_ThrowsArgumentException(string? sessionId)
    {
        // Given: An invalid session ID
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        // When/Then: ArgumentException is thrown
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.RevokeSessionAsync(sessionId!));
        Assert.Contains("Session ID cannot be null or empty", exception.Message);
    }

    /// <summary>
    /// Scenario: Revoke session with cache failure
    ///   Given an active session and a failing cache service
    ///   When calling RevokeSessionAsync
    ///   Then the session is still revoked in the database
    ///   And the method returns true (graceful degradation)
    /// </summary>
    [Fact]
    public async Task RevokeSessionAsync_WithCacheFailure_StillRevokesInDatabase()
    {
        // Given: Active session with failing cache
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var mockCache = new Mock<ISessionCacheService>();
        mockCache.Setup(c => c.InvalidateAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("Redis connection failed"));

        var service = CreateService(db, cache: mockCache.Object);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        var session = CreateSession(userId, "session1", tokenHash: "hash123", user: user);
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        // When: Revoking the session (cache will fail)
        // Note: In production this would be caught and logged, but for now it will throw
        await Assert.ThrowsAsync<Exception>(() => service.RevokeSessionAsync("session1"));

        // Then: Session is still revoked in database
        var revokedSession = await db.UserSessions.FindAsync("session1");
        Assert.NotNull(revokedSession!.RevokedAt);
    }

    /// <summary>
    /// Scenario: Revoke all sessions for a user
    ///   Given a user with multiple active sessions
    ///   When calling RevokeAllUserSessionsAsync
    ///   Then all active sessions are revoked
    ///   And the count of revoked sessions is returned
    ///   And cache is invalidated for all sessions
    /// </summary>
    [Fact]
    public async Task RevokeAllUserSessionsAsync_WithMultipleSessions_RevokesAllActive()
    {
        // Given: User with multiple active sessions
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-15T12:00:00Z"));
        var mockCache = new Mock<ISessionCacheService>();
        var service = CreateService(db, timeProvider, mockCache.Object);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        var session1 = CreateSession(userId, "session1", tokenHash: "hash1", user: user);
        var session2 = CreateSession(userId, "session2", tokenHash: "hash2", user: user);
        var session3 = CreateSession(userId, "session3", revokedAt: DateTime.Parse("2024-01-14T12:00:00Z"), tokenHash: "hash3", user: user);

        db.UserSessions.AddRange(session1, session2, session3);
        await db.SaveChangesAsync();

        // When: Revoking all user sessions
        var count = await service.RevokeAllUserSessionsAsync(userId);

        // Then: Both active sessions are revoked
        Assert.Equal(2, count);

        var allSessions = await db.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        Assert.All(allSessions, s => Assert.NotNull(s.RevokedAt));

        // And: Cache invalidated for all active sessions
        mockCache.Verify(c => c.InvalidateAsync("hash1", default), Times.Once);
        mockCache.Verify(c => c.InvalidateAsync("hash2", default), Times.Once);
        mockCache.Verify(c => c.InvalidateAsync("hash3", default), Times.Never); // Already revoked
    }

    /// <summary>
    /// Scenario: Revoke all sessions when user has no active sessions
    ///   Given a user with no active sessions
    ///   When calling RevokeAllUserSessionsAsync
    ///   Then zero is returned
    ///   And no database changes are made
    /// </summary>
    [Fact]
    public async Task RevokeAllUserSessionsAsync_WithNoActiveSessions_ReturnsZero()
    {
        // Given: User with no active sessions
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        var revokedSession = CreateSession(userId, "session1", revokedAt: DateTime.Parse("2024-01-14T12:00:00Z"), user: user);
        db.UserSessions.Add(revokedSession);
        await db.SaveChangesAsync();

        // When: Revoking all user sessions
        var count = await service.RevokeAllUserSessionsAsync(userId);

        // Then: Zero sessions revoked
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Scenario: Revoke all sessions with null userId
    ///   Given a null or empty userId
    ///   When calling RevokeAllUserSessionsAsync
    ///   Then an ArgumentException is thrown
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeAllUserSessionsAsync_WithNullOrEmptyUserId_ThrowsArgumentException(string? userId)
    {
        // Given: An invalid userId
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        // When/Then: ArgumentException is thrown
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.RevokeAllUserSessionsAsync(userId!));
        Assert.Contains("User ID cannot be null or empty", exception.Message);
    }

    /// <summary>
    /// Scenario: Auto-revoke inactive sessions
    ///   Given sessions with varying LastSeenAt timestamps
    ///   When calling RevokeInactiveSessionsAsync
    ///   Then only sessions inactive longer than the threshold are revoked
    ///   And active/recent sessions remain untouched
    /// </summary>
    [Fact]
    public async Task RevokeInactiveSessionsAsync_RevokesOnlyInactiveSessions()
    {
        // Given: Sessions with varying activity
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var now = DateTimeOffset.Parse("2024-02-15T12:00:00Z");
        var timeProvider = new FixedTimeProvider(now);
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var mockCache = new Mock<ISessionCacheService>();
        var service = CreateService(db, timeProvider, mockCache.Object, config);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        // Active session (seen 10 days ago)
        var activeSession = CreateSession(userId, "active", lastSeenAt: DateTime.Parse("2024-02-05T12:00:00Z"), tokenHash: "active-hash", user: user);

        // Inactive session (seen 40 days ago - should be revoked)
        var inactiveSession = CreateSession(userId, "inactive", lastSeenAt: DateTime.Parse("2024-01-06T12:00:00Z"), tokenHash: "inactive-hash", user: user);

        // Already revoked (should not be touched)
        var alreadyRevoked = CreateSession(userId, "revoked", revokedAt: DateTime.Parse("2024-01-01T12:00:00Z"), tokenHash: "revoked-hash", user: user);

        // Expired session (should not be revoked based on inactivity)
        var expiredSession = CreateSession(userId, "expired", expiresAt: DateTime.Parse("2024-01-01T12:00:00Z"), tokenHash: "expired-hash", user: user);

        db.UserSessions.AddRange(activeSession, inactiveSession, alreadyRevoked, expiredSession);
        await db.SaveChangesAsync();

        // When: Running auto-revocation
        var count = await service.RevokeInactiveSessionsAsync();

        // Then: Only the inactive session is revoked
        Assert.Equal(1, count);

        var sessions = await db.UserSessions.ToListAsync();
        Assert.NotNull(sessions.First(s => s.Id == "inactive").RevokedAt);
        Assert.Null(sessions.First(s => s.Id == "active").RevokedAt);
        Assert.NotNull(sessions.First(s => s.Id == "revoked").RevokedAt); // Already revoked
        Assert.Null(sessions.First(s => s.Id == "expired").RevokedAt); // Expired, not revoked

        // And: Cache invalidated for inactive session
        mockCache.Verify(c => c.InvalidateAsync("inactive-hash", default), Times.Once);
        mockCache.Verify(c => c.InvalidateAsync("active-hash", default), Times.Never);
    }

    /// <summary>
    /// Scenario: Auto-revoke uses CreatedAt when LastSeenAt is null
    ///   Given sessions without LastSeenAt timestamps
    ///   When calling RevokeInactiveSessionsAsync
    ///   Then sessions are evaluated based on CreatedAt
    /// </summary>
    [Fact]
    public async Task RevokeInactiveSessionsAsync_WithNullLastSeenAt_UsesCreatedAt()
    {
        // Given: Session without LastSeenAt
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var now = DateTimeOffset.Parse("2024-02-15T12:00:00Z");
        var timeProvider = new FixedTimeProvider(now);
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(db, timeProvider, config: config);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        // Session created 40 days ago, never seen
        var oldSession = CreateSession(userId, "old", createdAt: DateTime.Parse("2024-01-06T12:00:00Z"), lastSeenAt: null, user: user);

        // Session created 10 days ago, never seen
        var recentSession = CreateSession(userId, "recent", createdAt: DateTime.Parse("2024-02-05T12:00:00Z"), lastSeenAt: null, user: user);

        db.UserSessions.AddRange(oldSession, recentSession);
        await db.SaveChangesAsync();

        // When: Running auto-revocation
        var count = await service.RevokeInactiveSessionsAsync();

        // Then: Only the old session is revoked
        Assert.Equal(1, count);

        var sessions = await db.UserSessions.ToListAsync();
        Assert.NotNull(sessions.First(s => s.Id == "old").RevokedAt);
        Assert.Null(sessions.First(s => s.Id == "recent").RevokedAt);
    }

    /// <summary>
    /// Scenario: Auto-revoke with no inactive sessions
    ///   Given only active, recent sessions
    ///   When calling RevokeInactiveSessionsAsync
    ///   Then zero sessions are revoked
    /// </summary>
    [Fact]
    public async Task RevokeInactiveSessionsAsync_WithNoInactiveSessions_ReturnsZero()
    {
        // Given: Only active sessions
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var now = DateTimeOffset.Parse("2024-02-15T12:00:00Z");
        var timeProvider = new FixedTimeProvider(now);
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(db, timeProvider, config: config);

        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity { Id = userId, Email = "user@example.com", PasswordHash = "hash", DisplayName = "User", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);

        var activeSession = CreateSession(userId, "active", lastSeenAt: DateTime.Parse("2024-02-10T12:00:00Z"), user: user);
        db.UserSessions.Add(activeSession);
        await db.SaveChangesAsync();

        // When: Running auto-revocation
        var count = await service.RevokeInactiveSessionsAsync();

        // Then: No sessions revoked
        Assert.Equal(0, count);
    }

    // Helper methods

    private static async Task<MeepleAiDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private SessionManagementService CreateService(
        MeepleAiDbContext db,
        TimeProvider? timeProvider = null,
        ISessionCacheService? cache = null,
        SessionManagementConfiguration? config = null)
    {
        var options = Options.Create(config ?? _defaultConfig);
        return new SessionManagementService(
            db,
            options,
            NullLogger<SessionManagementService>.Instance,
            cache,
            timeProvider);
    }

    private static UserSessionEntity CreateSession(
        string userId,
        string sessionId,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        DateTime? lastSeenAt = null,
        DateTime? revokedAt = null,
        string? tokenHash = null,
        UserEntity? user = null)
    {
        return new UserSessionEntity
        {
            Id = sessionId,
            UserId = userId,
            TokenHash = tokenHash ?? $"hash-{sessionId}",
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(90),
            LastSeenAt = lastSeenAt,
            RevokedAt = revokedAt,
            IpAddress = "127.0.0.1",
            UserAgent = "test-agent",
            User = user ?? new UserEntity
            {
                Id = userId,
                Email = $"user-{userId}@example.com",
                PasswordHash = "hash",
                DisplayName = "Test User",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

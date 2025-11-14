using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for SessionRepository using Testcontainers with real PostgreSQL.
/// Tests session lifecycle, expiration queries, and token-based lookups.
/// </summary>
[Collection("Integration")]
public class SessionRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private SessionRepository? _repository;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public async ValueTask InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_session_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        await _dbContext.Database.MigrateAsync();

        _repository = new SessionRepository(_dbContext, _timeProvider);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    #region GetByTokenHashAsync Tests

    [Fact]
    public async Task Test01_GetByTokenHashAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTokenHashAsync(session.TokenHash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
        Assert.Equal(session.TokenHash, result.TokenHash);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task Test02_GetByTokenHashAsync_NonExistingToken_ReturnsNull()
    {
        // Arrange
        var nonExistentHash = "nonexistent_hash_123";

        // Act
        var result = await _repository!.GetByTokenHashAsync(nonExistentHash);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task Test03_GetByUserIdAsync_NoSessions_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var sessions = await _repository!.GetByUserIdAsync(userId);

        // Assert
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task Test04_GetByUserIdAsync_MultipleSessions_ReturnsAllOrdered()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session1 = CreateTestSession(userId);
        await Task.Delay(10); // Ensure different timestamps
        var session2 = CreateTestSession(userId);
        await Task.Delay(10);
        var session3 = CreateTestSession(userId);

        await _repository!.AddAsync(session1);
        await _repository.AddAsync(session2);
        await _repository.AddAsync(session3);
        await _dbContext!.SaveChangesAsync();

        // Act
        var sessions = await _repository.GetByUserIdAsync(userId);

        // Assert
        Assert.Equal(3, sessions.Count);
        // Should be ordered by CreatedAt descending
        Assert.True(sessions[0].CreatedAt >= sessions[1].CreatedAt);
        Assert.True(sessions[1].CreatedAt >= sessions[2].CreatedAt);
    }

    [Fact]
    public async Task Test05_GetByUserIdAsync_MultipleUsers_FiltersCorrectly()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var session1 = CreateTestSession(user1Id);
        var session2 = CreateTestSession(user1Id);
        var session3 = CreateTestSession(user2Id);

        await _repository!.AddAsync(session1);
        await _repository.AddAsync(session2);
        await _repository.AddAsync(session3);
        await _dbContext!.SaveChangesAsync();

        // Act
        var user1Sessions = await _repository.GetByUserIdAsync(user1Id);
        var user2Sessions = await _repository.GetByUserIdAsync(user2Id);

        // Assert
        Assert.Equal(2, user1Sessions.Count);
        Assert.Single(user2Sessions);
    }

    #endregion

    #region GetActiveSessionsByUserIdAsync Tests

    [Fact]
    public async Task Test06_GetActiveSessionsByUserIdAsync_OnlyActiveSessions_ReturnsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);

        await _repository!.AddAsync(session1);
        await _repository.AddAsync(session2);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, activeSessions.Count);
    }

    [Fact]
    public async Task Test07_GetActiveSessionsByUserIdAsync_ExpiredSessions_Excluded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeSession = CreateTestSession(userId);
        var expiredSession = CreateTestSession(userId, TimeSpan.FromDays(-1)); // Already expired

        await _repository!.AddAsync(activeSession);
        await _repository.AddAsync(expiredSession);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task Test08_GetActiveSessionsByUserIdAsync_RevokedSessions_Excluded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeSession = CreateTestSession(userId);
        var revokedSession = CreateTestSession(userId);
        revokedSession.Revoke();

        await _repository!.AddAsync(activeSession);
        await _repository.AddAsync(revokedSession);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task Test09_GetActiveSessionsByUserIdAsync_MixedSessions_FiltersCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var active1 = CreateTestSession(userId);
        var active2 = CreateTestSession(userId);
        var expired = CreateTestSession(userId, TimeSpan.FromDays(-1));
        var revoked = CreateTestSession(userId);
        revoked.Revoke();

        await _repository!.AddAsync(active1);
        await _repository.AddAsync(active2);
        await _repository.AddAsync(expired);
        await _repository.AddAsync(revoked);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, activeSessions.Count);
        Assert.Contains(activeSessions, s => s.Id == active1.Id);
        Assert.Contains(activeSessions, s => s.Id == active2.Id);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task Test10_AddAsync_NewSession_PersistsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId, ipAddress: "192.168.1.1", userAgent: "TestBrowser/1.0");

        // Act
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
        Assert.Equal(session.TokenHash, persisted.TokenHash);
        Assert.Equal("192.168.1.1", persisted.IpAddress);
        Assert.Equal("TestBrowser/1.0", persisted.UserAgent);
    }

    [Fact]
    public async Task Test11_AddAsync_SessionWithMetadata_StoresAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(
            userId,
            ipAddress: "10.0.0.1",
            userAgent: "Mozilla/5.0"
        );

        // Act
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Equal("10.0.0.1", persisted.IpAddress);
        Assert.Equal("Mozilla/5.0", persisted.UserAgent);
        Assert.NotNull(persisted.CreatedAt);
        Assert.NotNull(persisted.ExpiresAt);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Test12_UpdateAsync_LastSeenAt_UpdatesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act
        session.UpdateLastSeen();
        await _repository.UpdateAsync(session);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastSeenAt);
    }

    [Fact]
    public async Task Test13_UpdateAsync_RevokeSession_PersistsRevocation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act
        session.Revoke();
        await _repository.UpdateAsync(session);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.RevokedAt);
    }

    #endregion

    #region RevokeAllUserSessionsAsync Tests

    [Fact]
    public async Task Test14_RevokeAllUserSessionsAsync_MultipleSessions_RevokesAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);
        var session3 = CreateTestSession(userId);

        await _repository!.AddAsync(session1);
        await _repository.AddAsync(session2);
        await _repository.AddAsync(session3);
        await _dbContext!.SaveChangesAsync();

        // Act
        await _repository.RevokeAllUserSessionsAsync(userId);

        // Assert
        var allSessions = await _dbContext!.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        Assert.Equal(3, allSessions.Count);
        Assert.All(allSessions, s => Assert.NotNull(s.RevokedAt));
    }

    [Fact]
    public async Task Test15_RevokeAllUserSessionsAsync_AlreadyRevokedSessions_NoEffect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);
        session2.Revoke();

        await _repository!.AddAsync(session1);
        await _repository.AddAsync(session2);
        await _dbContext!.SaveChangesAsync();

        var originalRevokedAt = session2.RevokedAt;

        // Act
        await _repository.RevokeAllUserSessionsAsync(userId);

        // Assert
        var allSessions = await _dbContext!.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        Assert.Equal(2, allSessions.Count);

        var session1Updated = allSessions.First(s => s.Id == session1.Id);
        var session2Updated = allSessions.First(s => s.Id == session2.Id);

        Assert.NotNull(session1Updated.RevokedAt);
        Assert.Equal(originalRevokedAt, session2Updated.RevokedAt); // Should not change
    }

    [Fact]
    public async Task Test16_RevokeAllUserSessionsAsync_MultipleUsers_OnlyTargetUser()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var user1Session1 = CreateTestSession(user1Id);
        var user1Session2 = CreateTestSession(user1Id);
        var user2Session = CreateTestSession(user2Id);

        await _repository!.AddAsync(user1Session1);
        await _repository.AddAsync(user1Session2);
        await _repository.AddAsync(user2Session);
        await _dbContext!.SaveChangesAsync();

        // Act
        await _repository.RevokeAllUserSessionsAsync(user1Id);

        // Assert
        var user1Sessions = await _dbContext!.UserSessions.Where(s => s.UserId == user1Id).ToListAsync();
        var user2Sessions = await _dbContext.UserSessions.Where(s => s.UserId == user2Id).ToListAsync();

        Assert.All(user1Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.All(user2Sessions, s => Assert.Null(s.RevokedAt));
    }

    #endregion

    #region Mapping Tests

    [Fact]
    public async Task Test17_Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(
            userId,
            ipAddress: "203.0.113.1",
            userAgent: "TestAgent/2.0"
        );

        // Act
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Equal(session.Id, persisted.Id);
        Assert.Equal(session.UserId, persisted.UserId);
        Assert.Equal(session.TokenHash, persisted.TokenHash);
        Assert.Equal(session.IpAddress, persisted.IpAddress);
        Assert.Equal(session.UserAgent, persisted.UserAgent);
        Assert.Equal(session.CreatedAt, persisted.CreatedAt);
        Assert.Equal(session.ExpiresAt, persisted.ExpiresAt);
    }

    [Fact]
    public async Task Test18_Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByTokenHashAsync(session.TokenHash);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
        Assert.Equal(session.UserId, retrieved.UserId);
        Assert.Equal(session.TokenHash, retrieved.TokenHash);
        Assert.Equal(session.IpAddress, retrieved.IpAddress);
        Assert.Equal(session.UserAgent, retrieved.UserAgent);
    }

    #endregion

    #region Expiration Query Tests

    [Fact]
    public async Task Test19_ExpirationQuery_EdgeCase_ExactExpirationTime()
    {
        // Arrange
        var userId = Guid.NewGuid();
        // Create session that expires in 1 second
        var session = CreateTestSession(userId, TimeSpan.FromSeconds(1));
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act - Check active immediately
        var activeNow = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Wait for expiration
        await Task.Delay(1500);

        // Check active after expiration
        var activeAfter = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeNow);
        Assert.Empty(activeAfter);
    }

    [Fact]
    public async Task Test20_ExpirationQuery_FutureSessions_IncludedInActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId, TimeSpan.FromDays(30));
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeSessions);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Test21_ConcurrentTokenLookups_NoConflicts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act - Multiple concurrent token lookups
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _repository.GetByTokenHashAsync(session.TokenHash))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(session.Id, result.Id);
        });
    }

    [Fact]
    public async Task Test22_ConcurrentRevocations_Idempotent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act - Multiple concurrent revocations (should be idempotent)
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _repository.RevokeAllUserSessionsAsync(userId))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var revokedSession = await _dbContext!.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(revokedSession);
        Assert.NotNull(revokedSession.RevokedAt);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Test23_NullableFields_IpAddress_HandledCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId, ipAddress: null);

        // Act
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.IpAddress);
    }

    [Fact]
    public async Task Test24_NullableFields_UserAgent_HandledCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId, userAgent: null);

        // Act
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.UserAgent);
    }

    [Fact]
    public async Task Test25_LongUserAgent_PersistsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var longUserAgent = new string('A', 500);
        var session = CreateTestSession(userId, userAgent: longUserAgent);

        // Act
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Equal(longUserAgent, persisted.UserAgent);
    }

    [Fact]
    public async Task Test26_AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId);
        await _repository!.AddAsync(session);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByTokenHashAsync(session.TokenHash);

        // Modify retrieved object
        retrieved!.UpdateLastSeen();

        // SaveChanges without explicit Update call
        await _dbContext!.SaveChangesAsync();

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.Null(reloaded!.LastSeenAt); // Should remain null
    }

    #endregion

    #region Helper Methods

    private static Session CreateTestSession(
        Guid userId,
        TimeSpan? lifetime = null,
        string? ipAddress = "127.0.0.1",
        string? userAgent = "TestAgent/1.0")
    {
        var token = SessionToken.Generate();
        return new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: token,
            lifetime: lifetime ?? TimeSpan.FromDays(30),
            ipAddress: ipAddress,
            userAgent: userAgent
        );
    }

    #endregion
}

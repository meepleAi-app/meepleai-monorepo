using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for SessionRepository using Testcontainers with real PostgreSQL.
/// Tests session lifecycle, expiration queries, and token-based lookups.
/// </summary>
public class SessionRepositoryTests : IntegrationTestBase<SessionRepository>
{
    protected override string DatabaseName => "meepleai_session_test";

    protected override SessionRepository CreateRepository(MeepleAiDbContext dbContext)
        => new SessionRepository(dbContext, TimeProvider);

    #region GetByTokenHashAsync Tests

    [Fact]
    public async Task Test01_GetByTokenHashAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await Repository.GetByTokenHashAsync(session.TokenHash);

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
        await ResetDatabaseAsync();
        var nonExistentHash = "nonexistent_hash_123";

        // Act
        var result = await Repository.GetByTokenHashAsync(nonExistentHash);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task Test03_GetByUserIdAsync_NoSessions_ReturnsEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();

        // Act
        var sessions = await Repository.GetByUserIdAsync(userId);

        // Assert
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task Test04_GetByUserIdAsync_MultipleSessions_ReturnsAllOrdered()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        await Task.Delay(10); // Ensure different timestamps
        var session2 = CreateTestSession(userId);
        await Task.Delay(10);
        var session3 = CreateTestSession(userId);

        await Repository.AddAsync(session1);
        await Repository.AddAsync(session2);
        await Repository.AddAsync(session3);
        await DbContext.SaveChangesAsync();

        // Act
        var sessions = await Repository.GetByUserIdAsync(userId);

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
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync("user1@test.com");
        var user2Id = await CreateTestUserAsync("user2@test.com");

        var session1 = CreateTestSession(user1Id);
        var session2 = CreateTestSession(user1Id);
        var session3 = CreateTestSession(user2Id);

        await Repository.AddAsync(session1);
        await Repository.AddAsync(session2);
        await Repository.AddAsync(session3);
        await DbContext.SaveChangesAsync();

        // Act
        var user1Sessions = await Repository.GetByUserIdAsync(user1Id);
        var user2Sessions = await Repository.GetByUserIdAsync(user2Id);

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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);

        await Repository.AddAsync(session1);
        await Repository.AddAsync(session2);
        await DbContext.SaveChangesAsync();

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, activeSessions.Count);
    }

    [Fact]
    public async Task Test07_GetActiveSessionsByUserIdAsync_ExpiredSessions_Excluded()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var activeSession = CreateTestSession(userId);
        var expiredSession = CreateTestSession(userId, TimeSpan.FromDays(-1)); // Already expired

        await Repository.AddAsync(activeSession);
        await Repository.AddAsync(expiredSession);
        await DbContext.SaveChangesAsync();

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task Test08_GetActiveSessionsByUserIdAsync_RevokedSessions_Excluded()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var activeSession = CreateTestSession(userId);
        var revokedSession = CreateTestSession(userId);
        revokedSession.Revoke();

        await Repository.AddAsync(activeSession);
        await Repository.AddAsync(revokedSession);
        await DbContext.SaveChangesAsync();

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task Test09_GetActiveSessionsByUserIdAsync_MixedSessions_FiltersCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var active1 = CreateTestSession(userId);
        var active2 = CreateTestSession(userId);
        var expired = CreateTestSession(userId, TimeSpan.FromDays(-1));
        var revoked = CreateTestSession(userId);
        revoked.Revoke();

        await Repository.AddAsync(active1);
        await Repository.AddAsync(active2);
        await Repository.AddAsync(expired);
        await Repository.AddAsync(revoked);
        await DbContext.SaveChangesAsync();

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId);

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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, ipAddress: "192.168.1.1", userAgent: "TestBrowser/1.0");

        // Act
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(
            userId,
            ipAddress: "10.0.0.1",
            userAgent: "Mozilla/5.0"
        );

        // Act
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        // Act
        session.UpdateLastSeen();
        await Repository.UpdateAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastSeenAt);
    }

    [Fact]
    public async Task Test13_UpdateAsync_RevokeSession_PersistsRevocation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        // Act
        session.Revoke();
        await Repository.UpdateAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.RevokedAt);
    }

    #endregion

    #region RevokeAllUserSessionsAsync Tests

    [Fact]
    public async Task Test14_RevokeAllUserSessionsAsync_MultipleSessions_RevokesAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);
        var session3 = CreateTestSession(userId);

        await Repository.AddAsync(session1);
        await Repository.AddAsync(session2);
        await Repository.AddAsync(session3);
        await DbContext.SaveChangesAsync();

        // Act
        await Repository.RevokeAllUserSessionsAsync(userId);

        // Assert
        var allSessions = await DbContext.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        Assert.Equal(3, allSessions.Count);
        Assert.All(allSessions, s => Assert.NotNull(s.RevokedAt));
    }

    [Fact]
    public async Task Test15_RevokeAllUserSessionsAsync_AlreadyRevokedSessions_NoEffect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);
        session2.Revoke();

        await Repository.AddAsync(session1);
        await Repository.AddAsync(session2);
        await DbContext.SaveChangesAsync();

        var originalRevokedAt = session2.RevokedAt;

        // Act
        await Repository.RevokeAllUserSessionsAsync(userId);

        // Assert
        var allSessions = await DbContext.UserSessions.Where(s => s.UserId == userId).ToListAsync();
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
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync();
        var user2Id = await CreateTestUserAsync();

        var user1Session1 = CreateTestSession(user1Id);
        var user1Session2 = CreateTestSession(user1Id);
        var user2Session = CreateTestSession(user2Id);

        await Repository.AddAsync(user1Session1);
        await Repository.AddAsync(user1Session2);
        await Repository.AddAsync(user2Session);
        await DbContext.SaveChangesAsync();

        // Act
        await Repository.RevokeAllUserSessionsAsync(user1Id);

        // Assert
        var user1Sessions = await DbContext.UserSessions.Where(s => s.UserId == user1Id).ToListAsync();
        var user2Sessions = await DbContext.UserSessions.Where(s => s.UserId == user2Id).ToListAsync();

        Assert.All(user1Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.All(user2Sessions, s => Assert.Null(s.RevokedAt));
    }

    #endregion

    #region Mapping Tests

    [Fact]
    public async Task Test17_Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(
            userId,
            ipAddress: "203.0.113.1",
            userAgent: "TestAgent/2.0"
        );

        // Act
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await Repository.GetByTokenHashAsync(session.TokenHash);

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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        // Create session that expires in 1 second
        var session = CreateTestSession(userId, TimeSpan.FromSeconds(1));
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act - Check active immediately
        var activeNow = await Repository.GetActiveSessionsByUserIdAsync(userId);

        // Wait for expiration
        await Task.Delay(1500);

        // Check active after expiration
        var activeAfter = await Repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeNow);
        Assert.Empty(activeAfter);
    }

    [Fact]
    public async Task Test20_ExpirationQuery_FutureSessions_IncludedInActive()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, TimeSpan.FromDays(30));
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId);

        // Assert
        Assert.Single(activeSessions);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Test21_ConcurrentTokenLookups_NoConflicts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act - Multiple concurrent token lookups using independent repositories
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            return await repo.GetByTokenHashAsync(session.TokenHash);
        }).ToArray();

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
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act - Multiple concurrent revocations using independent repositories
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            await repo.RevokeAllUserSessionsAsync(userId);
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var revokedSession = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(revokedSession);
        Assert.NotNull(revokedSession.RevokedAt);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Test23_NullableFields_IpAddress_HandledCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, ipAddress: null);

        // Act
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.IpAddress);
    }

    [Fact]
    public async Task Test24_NullableFields_UserAgent_HandledCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, userAgent: null);

        // Act
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.UserAgent);
    }

    [Fact]
    public async Task Test25_LongUserAgent_PersistsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var longUserAgent = new string('A', 500);
        var session = CreateTestSession(userId, userAgent: longUserAgent);

        // Act
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted);
        Assert.Equal(longUserAgent, persisted.UserAgent);
    }

    [Fact]
    public async Task Test26_AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await Repository.GetByTokenHashAsync(session.TokenHash);

        // Modify retrieved object
        retrieved!.UpdateLastSeen();

        // SaveChanges without explicit Update call
        await DbContext.SaveChangesAsync();

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.Null(reloaded!.LastSeenAt); // Should remain null
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateTestUserAsync(string email = "test@example.com")
    {
        var userId = Guid.NewGuid();
        var user = new User(
            id: userId,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.User
        );

        // Use UserRepository or direct DbSet access with correct entity type
        var userEntity = new Api.Infrastructure.Entities.UserEntity
        {
            Id = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash.Value,
            Role = user.Role.Value,
            CreatedAt = user.CreatedAt
        };

        DbContext.Set<Api.Infrastructure.Entities.UserEntity>().Add(userEntity);
        await DbContext.SaveChangesAsync();

        return userId;
    }

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
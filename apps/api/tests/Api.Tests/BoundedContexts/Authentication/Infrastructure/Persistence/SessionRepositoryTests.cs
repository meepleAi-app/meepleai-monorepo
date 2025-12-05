using System.Threading;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
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
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    protected override string DatabaseName => "meepleai_session_test";

    protected override SessionRepository CreateRepository(MeepleAiDbContext dbContext)
        => new SessionRepository(dbContext, MockEventCollector.Object, TimeProvider);
    [Fact]
    public async Task GetByTokenHashAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await Repository.GetByTokenHashAsync(session.TokenHash, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
        Assert.Equal(session.TokenHash, result.TokenHash);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task GetByTokenHashAsync_NonExistingToken_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentHash = "nonexistent_hash_123";

        // Act
        var result = await Repository.GetByTokenHashAsync(nonExistentHash, TestCancellationToken);

        // Assert
        Assert.Null(result);
    }
    [Fact]
    public async Task GetByUserIdAsync_NoSessions_ReturnsEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();

        // Act
        var sessions = await Repository.GetByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetByUserIdAsync_MultipleSessions_ReturnsAllOrdered()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        await Task.Delay(TestConstants.Timing.TinyDelay); // Ensure different timestamps
        var session2 = CreateTestSession(userId);
        await Task.Delay(TestConstants.Timing.TinyDelay);
        var session3 = CreateTestSession(userId);

        await Repository.AddAsync(session1, TestCancellationToken);
        await Repository.AddAsync(session2, TestCancellationToken);
        await Repository.AddAsync(session3, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var sessions = await Repository.GetByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Equal(3, sessions.Count);
        // Should be ordered by CreatedAt descending
        Assert.True(sessions[0].CreatedAt >= sessions[1].CreatedAt);
        Assert.True(sessions[1].CreatedAt >= sessions[2].CreatedAt);
    }

    [Fact]
    public async Task GetByUserIdAsync_MultipleUsers_FiltersCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync("user1@test.com");
        var user2Id = await CreateTestUserAsync("user2@test.com");

        var session1 = CreateTestSession(user1Id);
        var session2 = CreateTestSession(user1Id);
        var session3 = CreateTestSession(user2Id);

        await Repository.AddAsync(session1, TestCancellationToken);
        await Repository.AddAsync(session2, TestCancellationToken);
        await Repository.AddAsync(session3, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var user1Sessions = await Repository.GetByUserIdAsync(user1Id, TestCancellationToken);
        var user2Sessions = await Repository.GetByUserIdAsync(user2Id, TestCancellationToken);

        // Assert
        Assert.Equal(2, user1Sessions.Count);
        Assert.Single(user2Sessions);
    }
    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_OnlyActiveSessions_ReturnsAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);

        await Repository.AddAsync(session1, TestCancellationToken);
        await Repository.AddAsync(session2, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Equal(2, activeSessions.Count);
    }

    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_ExpiredSessions_Excluded()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var activeSession = CreateTestSession(userId);
        var expiredSession = CreateTestSession(userId, TimeSpan.FromDays(-1)); // Already expired

        await Repository.AddAsync(activeSession, TestCancellationToken);
        await Repository.AddAsync(expiredSession, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_RevokedSessions_Excluded()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var activeSession = CreateTestSession(userId);
        var revokedSession = CreateTestSession(userId);
        revokedSession.Revoke();

        await Repository.AddAsync(activeSession, TestCancellationToken);
        await Repository.AddAsync(revokedSession, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_MixedSessions_FiltersCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var active1 = CreateTestSession(userId);
        var active2 = CreateTestSession(userId);
        var expired = CreateTestSession(userId, TimeSpan.FromDays(-1));
        var revoked = CreateTestSession(userId);
        revoked.Revoke();

        await Repository.AddAsync(active1, TestCancellationToken);
        await Repository.AddAsync(active2, TestCancellationToken);
        await Repository.AddAsync(expired, TestCancellationToken);
        await Repository.AddAsync(revoked, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Equal(2, activeSessions.Count);
        Assert.Contains(activeSessions, s => s.Id == active1.Id);
        Assert.Contains(activeSessions, s => s.Id == active2.Id);
    }
    [Fact]
    public async Task AddAsync_NewSession_PersistsSuccessfully()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, ipAddress: "192.168.1.1", userAgent: "TestBrowser/1.0");

        // Act
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
        Assert.Equal(session.TokenHash, persisted.TokenHash);
        Assert.Equal("192.168.1.1", persisted.IpAddress);
        Assert.Equal("TestBrowser/1.0", persisted.UserAgent);
    }

    [Fact]
    public async Task AddAsync_SessionWithMetadata_StoresAllFields()
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
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal("10.0.0.1", persisted.IpAddress);
        Assert.Equal("Mozilla/5.0", persisted.UserAgent);
        Assert.NotNull(persisted.CreatedAt);
        Assert.NotNull(persisted.ExpiresAt);
    }
    [Fact]
    public async Task UpdateAsync_LastSeenAt_UpdatesCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        DbContext.ChangeTracker.Clear();

        // Act
        session.UpdateLastSeen();
        await Repository.UpdateAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastSeenAt);
    }

    [Fact]
    public async Task UpdateAsync_RevokeSession_PersistsRevocation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        DbContext.ChangeTracker.Clear();

        // Act
        session.Revoke();
        await Repository.UpdateAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(updated);
        Assert.NotNull(updated.RevokedAt);
    }
    [Fact]
    public async Task RevokeAllUserSessionsAsync_MultipleSessions_RevokesAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);
        var session3 = CreateTestSession(userId);

        await Repository.AddAsync(session1, TestCancellationToken);
        await Repository.AddAsync(session2, TestCancellationToken);
        await Repository.AddAsync(session3, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        await Repository.RevokeAllUserSessionsAsync(userId, TestCancellationToken);

        // Assert
        var allSessions = await DbContext.UserSessions.Where(s => s.UserId == userId).ToListAsync(TestCancellationToken);
        Assert.Equal(3, allSessions.Count);
        Assert.All(allSessions, s => Assert.NotNull(s.RevokedAt));
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_AlreadyRevokedSessions_NoEffect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session1 = CreateTestSession(userId);
        var session2 = CreateTestSession(userId);
        session2.Revoke();

        await Repository.AddAsync(session1, TestCancellationToken);
        await Repository.AddAsync(session2, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var originalRevokedAt = session2.RevokedAt;

        // Act
        await Repository.RevokeAllUserSessionsAsync(userId, TestCancellationToken);

        // Assert
        var allSessions = await DbContext.UserSessions.Where(s => s.UserId == userId).ToListAsync(TestCancellationToken);
        Assert.Equal(2, allSessions.Count);

        var session1Updated = allSessions.First(s => s.Id == session1.Id);
        var session2Updated = allSessions.First(s => s.Id == session2.Id);

        Assert.NotNull(session1Updated.RevokedAt);
        Assert.Equal(originalRevokedAt, session2Updated.RevokedAt); // Should not change
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_MultipleUsers_OnlyTargetUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync("user1@test.com");
        var user2Id = await CreateTestUserAsync("user2@test.com");

        var user1Session1 = CreateTestSession(user1Id);
        var user1Session2 = CreateTestSession(user1Id);
        var user2Session = CreateTestSession(user2Id);

        await Repository.AddAsync(user1Session1, TestCancellationToken);
        await Repository.AddAsync(user1Session2, TestCancellationToken);
        await Repository.AddAsync(user2Session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        await Repository.RevokeAllUserSessionsAsync(user1Id, TestCancellationToken);

        // Assert
        var user1Sessions = await DbContext.UserSessions.Where(s => s.UserId == user1Id).ToListAsync(TestCancellationToken);
        var user2Sessions = await DbContext.UserSessions.Where(s => s.UserId == user2Id).ToListAsync(TestCancellationToken);

        Assert.All(user1Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.All(user2Sessions, s => Assert.Null(s.RevokedAt));
    }
    [Fact]
    public async Task Mapping_DomainToPersistence_AllFieldsCorrect()
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
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
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
    public async Task Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await Repository.GetByTokenHashAsync(session.TokenHash, TestCancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
        Assert.Equal(session.UserId, retrieved.UserId);
        Assert.Equal(session.TokenHash, retrieved.TokenHash);
        Assert.Equal(session.IpAddress, retrieved.IpAddress);
        Assert.Equal(session.UserAgent, retrieved.UserAgent);
    }
    [Fact]
    public async Task ExpirationQuery_EdgeCase_ExactExpirationTime()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        // Create session that expires in 1 second
        var session = CreateTestSession(userId, AuthenticationTestConstants.SessionExpiry.VeryShort);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Check active immediately
        var activeNow = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Advance time past expiration (1 second + buffer)
        TimeProvider.Advance(AuthenticationTestConstants.TimeAdvance.PastVeryShortExpiry);

        // Check active after expiration
        var activeAfter = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Single(activeNow);
        Assert.Empty(activeAfter);
    }

    [Fact]
    public async Task ExpirationQuery_FutureSessions_IncludedInActive()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, TimeSpan.FromDays(30));
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeSessions = await Repository.GetActiveSessionsByUserIdAsync(userId, TestCancellationToken);

        // Assert
        Assert.Single(activeSessions);
    }
    [Fact]
    public async Task ConcurrentTokenLookups_NoConflicts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple concurrent token lookups using independent repositories
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            return await repo.GetByTokenHashAsync(session.TokenHash, TestCancellationToken);
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
    public async Task ConcurrentRevocations_Idempotent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple concurrent revocations using independent repositories
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            await repo.RevokeAllUserSessionsAsync(userId, TestCancellationToken);
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert - Use AsNoTracking to fetch fresh data from database (bypass change tracker cache)
        var revokedSession = await DbContext.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(revokedSession);
        Assert.NotNull(revokedSession.RevokedAt);
    }
    [Fact]
    public async Task NullableFields_IpAddress_HandledCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, ipAddress: null);

        // Act
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(persisted);
        Assert.Null(persisted.IpAddress);
    }

    [Fact]
    public async Task NullableFields_UserAgent_HandledCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId, userAgent: null);

        // Act
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(persisted);
        Assert.Null(persisted.UserAgent);
    }

    [Fact]
    public async Task LongUserAgent_PersistsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var longUserAgent = new string('A', 500);
        var session = CreateTestSession(userId, userAgent: longUserAgent);

        // Act
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - UserAgent is truncated to 256 chars (database column limit)
        var persisted = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(256, persisted.UserAgent?.Length);
        Assert.Equal(longUserAgent.Substring(0, 256), persisted.UserAgent);
    }

    [Fact]
    public async Task AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var session = CreateTestSession(userId);
        await Repository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await Repository.GetByTokenHashAsync(session.TokenHash, TestCancellationToken);

        // Modify retrieved object
        retrieved!.UpdateLastSeen();

        // SaveChanges without explicit Update call
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await DbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id, TestCancellationToken);
        Assert.Null(reloaded!.LastSeenAt); // Should remain null
    }
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
        await DbContext.SaveChangesAsync(TestCancellationToken);

        return userId;
    }

    private Session CreateTestSession(
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
            userAgent: userAgent,
            timeProvider: TimeProvider  // Use TestTimeProvider from base class
        );
    }
}


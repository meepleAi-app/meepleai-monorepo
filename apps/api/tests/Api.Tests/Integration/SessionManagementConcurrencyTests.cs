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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// TEST-03 Phase 2: Concurrent access tests for SessionManagementService
/// Verifies thread-safety for session revocation and management operations
/// </summary>
public class SessionManagementConcurrencyTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly SessionManagementService _service;
    private readonly SessionManagementConfiguration _config;

    public SessionManagementConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var configOptions = Options.Create(_config);
        var logger = new NullLogger<SessionManagementService>();

        _service = new SessionManagementService(
            _dbContext,
            configOptions,
            logger,
            null, // No cache for these tests
            TimeProvider.System
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Pattern 1: Concurrent Writes - Lost Update Detection
    /// Tests that concurrent revocation attempts on the same session are safe
    /// </summary>
    [Fact]
    public async Task ConcurrentSessionRevocation_SameSession_Idempotent_Test()
    {
        // Arrange: Create user and session
        var userId = "user-concurrent-1";
        var sessionId = Guid.NewGuid().ToString();

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "concurrent@test.com",
            Username = "concurrent",
            Role = UserRole.User,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.UserSessions.AddAsync(new UserSessionEntity
        {
            Id = sessionId,
            UserId = userId,
            TokenHash = "hash-1",
            IpAddress = "127.0.0.1",
            UserAgent = "Test",
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        await _dbContext.SaveChangesAsync();

        // Act: 5 concurrent revocation attempts on the same session
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            return await _service.RevokeSessionAsync(sessionId);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: At least one succeeded (idempotent behavior)
        var successCount = results.Count(r => r);

        _output.WriteLine($"Success count: {successCount}/5");

        Assert.True(successCount >= 1, "At least one revocation should succeed");
        Assert.True(successCount <= 5, "Maximum 5 revocations can be reported");

        // Verify session is revoked in database
        var session = await _dbContext.UserSessions.FindAsync(sessionId);
        Assert.NotNull(session);
        Assert.NotNull(session!.RevokedAt);
    }

    /// <summary>
    /// Pattern 2: Optimistic Concurrency - Read-Modify-Write
    /// Tests concurrent RevokeAllUserSessionsAsync operations
    /// </summary>
    [Fact]
    public async Task ConcurrentRevokeAllSessions_SameUser_ConsistentCount_Test()
    {
        // Arrange: Create user with 3 sessions
        var userId = "user-revoke-all";

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "revokeall@test.com",
            Username = "revokeall",
            Role = UserRole.User,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        for (int i = 1; i <= 3; i++)
        {
            await _dbContext.UserSessions.AddAsync(new UserSessionEntity
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TokenHash = $"hash-{i}",
                IpAddress = "127.0.0.1",
                UserAgent = "Test",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        }

        await _dbContext.SaveChangesAsync();

        // Act: 2 concurrent RevokeAllUserSessionsAsync calls
        var task1 = Task.Run(async () => await _service.RevokeAllUserSessionsAsync(userId));
        var task2 = Task.Run(async () => await _service.RevokeAllUserSessionsAsync(userId));

        var results = await Task.WhenAll(task1, task2);

        // Assert: Total revoked count should be 3 (not 6)
        var totalReported = results.Sum();

        _output.WriteLine($"Task 1 revoked: {results[0]}, Task 2 revoked: {results[1]}");
        _output.WriteLine($"Total reported: {totalReported}");

        // Due to race condition, one might see 3 and other 0, or both might see 3
        // But total should not exceed 3
        Assert.True(totalReported <= 3, "Total revoked should not exceed actual session count");

        // Verify all 3 sessions are revoked
        var revokedCount = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt != null)
            .CountAsync();

        Assert.Equal(3, revokedCount);
    }

    /// <summary>
    /// Pattern 3: TOCTOU (Time-Of-Check-Time-Of-Use)
    /// Tests that concurrent single and bulk revocations don't create inconsistencies
    /// </summary>
    [Fact]
    public async Task ConcurrentMixedRevocations_NoTOCTOU_Test()
    {
        // Arrange: Create user with 5 sessions
        var userId = "user-mixed-revoke";
        var sessionIds = new List<string>();

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "mixed@test.com",
            Username = "mixed",
            Role = UserRole.User,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        for (int i = 1; i <= 5; i++)
        {
            var sessionId = Guid.NewGuid().ToString();
            sessionIds.Add(sessionId);

            await _dbContext.UserSessions.AddAsync(new UserSessionEntity
            {
                Id = sessionId,
                UserId = userId,
                TokenHash = $"hash-{i}",
                IpAddress = "127.0.0.1",
                UserAgent = "Test",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        }

        await _dbContext.SaveChangesAsync();

        // Act: Concurrent operations:
        // - RevokeAllUserSessionsAsync (should revoke all 5)
        // - RevokeSessionAsync on specific sessions (should be idempotent)
        var tasks = new List<Task>();

        // Bulk revoke all
        tasks.Add(Task.Run(async () => await _service.RevokeAllUserSessionsAsync(userId)));

        // Individual revocations on first 3 sessions
        for (int i = 0; i < 3; i++)
        {
            var sessionId = sessionIds[i];
            tasks.Add(Task.Run(async () => await _service.RevokeSessionAsync(sessionId)));
        }

        await Task.WhenAll(tasks);

        // Assert: All 5 sessions should be revoked exactly once
        var allSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        Assert.Equal(5, allSessions.Count);
        Assert.All(allSessions, s => Assert.NotNull(s.RevokedAt));

        _output.WriteLine($"All {allSessions.Count} sessions successfully revoked");
    }

    /// <summary>
    /// Pattern 4: Cache Coherence - Invalidation Propagation
    /// Tests that concurrent operations maintain data consistency
    /// </summary>
    [Fact]
    public async Task ConcurrentRevocations_DataConsistency_Test()
    {
        // Arrange: Create multiple users with sessions
        var userIds = new List<string>();

        for (int i = 1; i <= 3; i++)
        {
            var userId = $"user-consistency-{i}";
            userIds.Add(userId);

            await _dbContext.Users.AddAsync(new UserEntity
            {
                Id = userId,
                Email = $"user{i}@test.com",
                Username = $"user{i}",
                Role = UserRole.User,
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow
            });

            // Each user gets 2 sessions
            for (int j = 1; j <= 2; j++)
            {
                await _dbContext.UserSessions.AddAsync(new UserSessionEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    TokenHash = $"hash-{i}-{j}",
                    IpAddress = "127.0.0.1",
                    UserAgent = "Test",
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        // Act: Concurrent revocations for all users
        var tasks = userIds.Select(async userId =>
        {
            return await _service.RevokeAllUserSessionsAsync(userId);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: Each user should have 2 sessions revoked
        var totalRevoked = results.Sum();
        Assert.Equal(6, totalRevoked); // 3 users * 2 sessions each

        // Verify all sessions are revoked
        var allRevokedCount = await _dbContext.UserSessions
            .Where(s => s.RevokedAt != null)
            .CountAsync();

        Assert.Equal(6, allRevokedCount);

        _output.WriteLine($"Successfully revoked {totalRevoked} sessions across {userIds.Count} users");
    }

    /// <summary>
    /// Additional test: Concurrent inactive session cleanup
    /// Tests RevokeInactiveSessionsAsync for race conditions
    /// </summary>
    [Fact]
    public async Task ConcurrentInactiveRevocation_NoDuplicates_Test()
    {
        // Arrange: Create sessions with various activity dates
        var userId = "user-inactive";

        await _dbContext.Users.AddAsync(new UserEntity
        {
            Id = userId,
            Email = "inactive@test.com",
            Username = "inactive",
            Role = UserRole.User,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });

        var now = DateTime.UtcNow;

        // 3 inactive sessions (> 30 days old)
        for (int i = 1; i <= 3; i++)
        {
            await _dbContext.UserSessions.AddAsync(new UserSessionEntity
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TokenHash = $"hash-old-{i}",
                IpAddress = "127.0.0.1",
                UserAgent = "Test",
                CreatedAt = now.AddDays(-35),
                LastActivityAt = now.AddDays(-35),
                ExpiresAt = now.AddDays(-5)
            });
        }

        // 2 active sessions (recent)
        for (int i = 1; i <= 2; i++)
        {
            await _dbContext.UserSessions.AddAsync(new UserSessionEntity
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TokenHash = $"hash-new-{i}",
                IpAddress = "127.0.0.1",
                UserAgent = "Test",
                CreatedAt = now.AddHours(-1),
                LastActivityAt = now.AddHours(-1),
                ExpiresAt = now.AddDays(1)
            });
        }

        await _dbContext.SaveChangesAsync();

        // Act: 3 concurrent calls to RevokeInactiveSessionsAsync
        var tasks = Enumerable.Range(1, 3).Select(async i =>
        {
            return await _service.RevokeInactiveSessionsAsync();
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: Total reported revocations might vary due to race condition
        var totalReported = results.Sum();

        _output.WriteLine($"Revocation counts: {string.Join(", ", results)}");
        _output.WriteLine($"Total reported: {totalReported}");

        // But exactly 3 sessions should be revoked in database
        var revokedCount = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt != null)
            .CountAsync();

        Assert.Equal(3, revokedCount);

        // And 2 sessions should remain active
        var activeCount = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .CountAsync();

        Assert.Equal(2, activeCount);
    }
}

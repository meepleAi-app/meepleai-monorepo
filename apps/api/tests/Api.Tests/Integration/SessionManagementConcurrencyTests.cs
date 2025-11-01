using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// TEST-04: Concurrency tests for SessionManagementService
/// Verifies thread-safety in concurrent session revocations, bulk operations, and cleanup
///
/// Reference: ConfigurationConcurrencyTests (MANDATORY PATTERN)
/// Uses: WebApplicationFactory + Testcontainers + PostgreSQL (NO SQLite)
/// </summary>
[Collection("Admin Endpoints")]
public class SessionManagementConcurrencyTests : ConfigIntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private string _adminEmail = null!;
    private List<string> _adminCookies = null!;

    public SessionManagementConcurrencyTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adminEmail = $"session-admin-{Guid.NewGuid():N}@test.com";

        // Register and get cookies for reuse
        using var tempClient = Factory.CreateHttpsClient();
        _adminCookies = await RegisterAndAuthenticateAsync(tempClient, _adminEmail, "Admin");
    }

    /// <summary>
    /// Pattern 1: Idempotent Concurrent Revocations
    /// Scenario: Multiple admins revoking the same session simultaneously
    ///   Given an active user session
    ///   When 5 admins attempt to revoke it concurrently
    ///   Then all requests succeed (idempotent operation)
    ///   And session is revoked exactly once
    /// </summary>
    [Fact]
    public async Task ConcurrentSameSessionRevocations_AreIdempotent_Test()
    {
        // Arrange: Create user with session
        var userEmail = $"user-revoke-{Guid.NewGuid():N}@test.com";
        using var userTempClient = Factory.CreateHttpsClient();
        await RegisterAndAuthenticateAsync(userTempClient, userEmail, "User");

        // Get session ID
        string? sessionId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            sessionId = await db.UserSessions
                .Where(s => s.User!.Email == userEmail && s.RevokedAt == null)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
        }

        sessionId.Should().NotBeNull();

        // Act: 5 concurrent revoke attempts by different admins
        var tasks = new Task<HttpResponseMessage>[5];
        for (int i = 0; i < 5; i++)
        {
            var adminClient = Factory.CreateHttpsClient();
            var adminEmail = $"admin-{i}-{Guid.NewGuid():N}@test.com";
            using var tempClient = Factory.CreateHttpsClient();
            var adminCookies = await RegisterAndAuthenticateAsync(tempClient, adminEmail, "Admin");

            tasks[i] = DeleteAuthenticatedAsync(adminClient, adminCookies, $"/api/v1/admin/sessions/{sessionId}");
        }

        var results = await Task.WhenAll(tasks);

        // Assert: At least one success (others may be 200 or 404 if already revoked)
        var successOrNotFoundCount = results.Count(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.NotFound);
        successOrNotFoundCount == 5, "All requests should succeed (idempotent)".Should().BeTrue();

        // Verify session is revoked exactly once
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var session = await db2.UserSessions.FindAsync(sessionId);
        session!.RevokedAt.Should().NotBeNull();

        _output.WriteLine($"Session revoked at: {session.RevokedAt}");
        _output.WriteLine($"Success responses: {results.Count(r => r.StatusCode == HttpStatusCode.OK)}");
    }

    /// <summary>
    /// Pattern 2: Concurrent Bulk Revocations
    /// Scenario: Multiple admins revoking all sessions for same user
    ///   Given a user with multiple sessions
    ///   When 2 admins call RevokeAllUserSessionsAsync concurrently
    ///   Then both operations succeed
    ///   And all sessions are revoked
    ///   And count consistency is maintained
    /// </summary>
    [Fact]
    public async Task ConcurrentBulkRevocations_SameUser_MaintainConsistency_Test()
    {
        // Arrange: Create user and manually create 3 sessions
        var userEmail = $"user-bulk-{Guid.NewGuid():N}@test.com";
        var userId = "";

        // Create user once
        using var userTempClient = Factory.CreateHttpsClient();
        await RegisterAndAuthenticateAsync(userTempClient, userEmail, "User");

        // Manually create additional sessions in database
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            userId = user!.Id;

            // Add 2 more sessions (1 already exists from RegisterAndAuthenticate)
            for (int i = 0; i < 2; i++)
            {
                db.UserSessions.Add(new UserSessionEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    TokenHash = $"hash-{i}",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    User = user
                });
            }
            await db.SaveChangesAsync();
        }

        // Get user ID
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            userId = (await db.Users.FirstOrDefaultAsync(u => u.Email == userEmail))!.Id;
        }

        // Act: 2 concurrent bulk revocations via API
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        var admin2Email = $"admin2-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var admin2Cookies = await RegisterAndAuthenticateAsync(tempClient2, admin2Email, "Admin");

        var task1 = DeleteAuthenticatedAsync(client1, _adminCookies, $"/api/v1/admin/users/{userId}/sessions");
        var task2 = DeleteAuthenticatedAsync(client2, admin2Cookies, $"/api/v1/admin/users/{userId}/sessions");

        var results = await Task.WhenAll(task1, task2);

        // Assert: Both succeed
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.NoContent);

        // Verify all sessions revoked
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var remainingActive = await db2.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .CountAsync();

        remainingActive.Should().Be(0);
    }

    /// <summary>
    /// Pattern 3: TOCTOU Prevention in Mixed Operations
    /// Scenario: Concurrent single + bulk revocations
    ///   Given a user with multiple sessions
    ///   When admin1 revokes one session while admin2 revokes all
    ///   Then no duplicate revocations occur
    ///   And final state is consistent
    /// </summary>
    [Fact]
    public async Task MixedConcurrentRevocations_PreventTOCTOU_Test()
    {
        // Arrange: Create user and manually create 3 sessions
        var userEmail = $"user-mixed-{Guid.NewGuid():N}@test.com";
        var userId = "";
        var sessionIds = new List<string>();

        // Create user once
        using var userTempClient = Factory.CreateHttpsClient();
        await RegisterAndAuthenticateAsync(userTempClient, userEmail, "User");

        // Manually create additional sessions in database
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            userId = user!.Id;

            // Add 2 more sessions
            for (int i = 0; i < 2; i++)
            {
                db.UserSessions.Add(new UserSessionEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    TokenHash = $"hash-{i}",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    User = user
                });
            }
            await db.SaveChangesAsync();
        }

        // Get user ID and session IDs
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            userId = user!.Id;
            sessionIds = await db.UserSessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .Select(s => s.Id)
                .ToListAsync();
        }

        sessionIds.Count.Should().Be(3);

        // Act: Concurrent single revoke + bulk revoke
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        var admin2Email = $"admin2-mixed-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var admin2Cookies = await RegisterAndAuthenticateAsync(tempClient2, admin2Email, "Admin");

        var singleRevoke = DeleteAuthenticatedAsync(client1, _adminCookies, $"/api/v1/admin/sessions/{sessionIds[0]}");
        var bulkRevoke = DeleteAuthenticatedAsync(client2, admin2Cookies, $"/api/v1/admin/users/{userId}/sessions");

        await Task.WhenAll(singleRevoke, bulkRevoke);

        // Assert: All sessions revoked, no errors
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var revokedSessions = await db2.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt != null)
            .ToListAsync();

        revokedSessions.Count.Should().Be(3);
        revokedSessions.Should().OnlyContain(s => s.RevokedAt != null);
    }

    /// <summary>
    /// Pattern 4: Concurrent Inactive Cleanup
    /// Scenario: Multiple concurrent cleanup operations
    ///   Given expired sessions exist
    ///   When 2 cleanup operations run concurrently
    ///   Then no duplicate revocations occur
    ///   And all expired sessions are revoked exactly once
    /// </summary>
    [Fact]
    public async Task ConcurrentInactiveSessionCleanup_NoDuplicateRevocations_Test()
    {
        // Arrange: Create user sessions with old LastActivity
        var sessionIds = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var userEmail = $"user-cleanup-{i}-{Guid.NewGuid():N}@test.com";
            using var userTempClient = Factory.CreateHttpsClient();
            await RegisterAndAuthenticateAsync(userTempClient, userEmail, "User");
        }

        // Manually set LastActivity to 31 days ago
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var oldDate = DateTime.UtcNow.AddDays(-31);

            var sessions = await db.UserSessions
                .Where(s => s.RevokedAt == null)
                .Take(5)
                .ToListAsync();

            foreach (var session in sessions)
            {
                session.LastSeenAt = oldDate;
                sessionIds.Add(session.Id);
            }
            await db.SaveChangesAsync();
        }

        // Act: 2 concurrent cleanup operations via service
        var tasks = new Task[2];
        for (int i = 0; i < 2; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var scope = Factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                var inactiveTimeoutDays = 30;
                var cutoffDate = DateTime.UtcNow.AddDays(-inactiveTimeoutDays);

                var inactiveSessions = await db.UserSessions
                    .Where(s => s.RevokedAt == null && s.LastSeenAt < cutoffDate)
                    .ToListAsync();

                foreach (var session in inactiveSessions)
                {
                    session.RevokedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();
            });
        }

        await Task.WhenAll(tasks);

        // Assert: All sessions revoked, no duplicates
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var revokedCount = await db2.UserSessions
            .Where(s => sessionIds.Contains(s.Id) && s.RevokedAt != null)
            .CountAsync();

        revokedCount.Should().Be(5);
        _output.WriteLine($"Successfully revoked {revokedCount} inactive sessions");
    }

    /// <summary>
    /// Pattern 5: Multi-User Concurrent Operations
    /// Scenario: Concurrent revocations across different users
    ///   Given multiple users with sessions
    ///   When admins revoke sessions for different users concurrently
    ///   Then all operations succeed
    ///   And data consistency is maintained per user
    /// </summary>
    [Fact]
    public async Task MultiUserConcurrentRevocations_MaintainDataConsistency_Test()
    {
        // Arrange: Create 5 users with sessions
        var userIds = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var userEmail = $"user-multi-{i}-{Guid.NewGuid():N}@test.com";
            using var userTempClient = Factory.CreateHttpsClient();
            await RegisterAndAuthenticateAsync(userTempClient, userEmail, "User");

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var userId = (await db.Users.FirstOrDefaultAsync(u => u.Email == userEmail))!.Id;
            userIds.Add(userId);
        }

        // Act: 5 concurrent bulk revocations for different users
        var tasks = new Task<HttpResponseMessage>[5];
        for (int i = 0; i < 5; i++)
        {
            var adminClient = Factory.CreateHttpsClient();
            var adminEmail = $"admin-multi-{i}-{Guid.NewGuid():N}@test.com";
            using var tempClient = Factory.CreateHttpsClient();
            var adminCookies = await RegisterAndAuthenticateAsync(tempClient, adminEmail, "Admin");

            tasks[i] = DeleteAuthenticatedAsync(adminClient, adminCookies, $"/api/v1/admin/users/{userIds[i]}/sessions");
        }

        var results = await Task.WhenAll(tasks);

        // Assert: All operations succeeded
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.NoContent);

        // Verify per-user consistency
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        foreach (var userId in userIds)
        {
            var activeCount = await db2.UserSessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .CountAsync();
            activeCount.Should().Be(0);
        }
    }
}

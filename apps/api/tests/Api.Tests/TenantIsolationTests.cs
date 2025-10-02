using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

public class TenantIsolationTests
{
    [Fact]
    public async Task GlobalQueryFilter_IsolatesTenantData()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        // Setup database
        await using (var setupContext = new MeepleAiDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            // Create two tenants
            var tenant1 = new TenantEntity { Id = "tenant-1", Name = "Tenant 1" };
            var tenant2 = new TenantEntity { Id = "tenant-2", Name = "Tenant 2" };
            setupContext.Tenants.AddRange(tenant1, tenant2);

            // Create games for each tenant
            var game1 = new GameEntity { Id = "game-1", TenantId = "tenant-1", Name = "Game 1", Tenant = tenant1 };
            var game2 = new GameEntity { Id = "game-2", TenantId = "tenant-2", Name = "Game 2", Tenant = tenant2 };
            setupContext.Games.AddRange(game1, game2);

            await setupContext.SaveChangesAsync();
        }

        // Test with tenant-1 context
        var tenantContext1 = new MockTenantContext("tenant-1");
        await using (var context1 = new MeepleAiDbContext(options, tenantContext1))
        {
            var games = await context1.Games.ToListAsync();

            // Should only see tenant-1's game
            Assert.Single(games);
            Assert.Equal("game-1", games[0].Id);
            Assert.Equal("tenant-1", games[0].TenantId);
        }

        // Test with tenant-2 context
        var tenantContext2 = new MockTenantContext("tenant-2");
        await using (var context2 = new MeepleAiDbContext(options, tenantContext2))
        {
            var games = await context2.Games.ToListAsync();

            // Should only see tenant-2's game
            Assert.Single(games);
            Assert.Equal("game-2", games[0].Id);
            Assert.Equal("tenant-2", games[0].TenantId);
        }

        // Test with no tenant context (should see all)
        await using (var contextNoTenant = new MeepleAiDbContext(options, null))
        {
            var games = await contextNoTenant.Games.ToListAsync();

            // Should see both games when no tenant context
            Assert.Equal(2, games.Count);
        }
    }

    [Fact]
    public async Task TenantIsolation_PreventsUnauthorizedAccess()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        // Setup
        await using (var setupContext = new MeepleAiDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            var tenant1 = new TenantEntity { Id = "tenant-alice", Name = "Alice's Tenant" };
            var tenant2 = new TenantEntity { Id = "tenant-bob", Name = "Bob's Tenant" };
            setupContext.Tenants.AddRange(tenant1, tenant2);

            var userAlice = new UserEntity
            {
                Id = "user-alice",
                TenantId = "tenant-alice",
                Email = "alice@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                Tenant = tenant1
            };

            var userBob = new UserEntity
            {
                Id = "user-bob",
                TenantId = "tenant-bob",
                Email = "bob@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                Tenant = tenant2
            };

            setupContext.Users.AddRange(userAlice, userBob);

            var gameAlice = new GameEntity
            {
                Id = "game-alice",
                TenantId = "tenant-alice",
                Name = "Alice's Game",
                Tenant = tenant1
            };

            var gameBob = new GameEntity
            {
                Id = "game-bob",
                TenantId = "tenant-bob",
                Name = "Bob's Game",
                Tenant = tenant2
            };

            setupContext.Games.AddRange(gameAlice, gameBob);
            await setupContext.SaveChangesAsync();
        }

        // Alice tries to query games
        var aliceContext = new MockTenantContext("tenant-alice");
        await using (var contextAlice = new MeepleAiDbContext(options, aliceContext))
        {
            var games = await contextAlice.Games.ToListAsync();
            var users = await contextAlice.Users.ToListAsync();

            // Alice should only see her own data
            Assert.Single(games);
            Assert.Equal("game-alice", games[0].Id);
            Assert.Single(users);
            Assert.Equal("user-alice", users[0].Id);

            // Try to directly query Bob's game by ID - should return null due to filter
            var bobGameQuery = await contextAlice.Games
                .FirstOrDefaultAsync(g => g.Id == "game-bob");
            Assert.Null(bobGameQuery);
        }

        // Bob tries to query games
        var bobContext = new MockTenantContext("tenant-bob");
        await using (var contextBob = new MeepleAiDbContext(options, bobContext))
        {
            var games = await contextBob.Games.ToListAsync();
            var users = await contextBob.Users.ToListAsync();

            // Bob should only see his own data
            Assert.Single(games);
            Assert.Equal("game-bob", games[0].Id);
            Assert.Single(users);
            Assert.Equal("user-bob", users[0].Id);

            // Try to directly query Alice's game by ID - should return null due to filter
            var aliceGameQuery = await contextBob.Games
                .FirstOrDefaultAsync(g => g.Id == "game-alice");
            Assert.Null(aliceGameQuery);
        }
    }

    [Fact]
    public async Task AuditLog_RecordsAccessDenied()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new MeepleAiDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            var tenant = new TenantEntity { Id = "tenant-test", Name = "Test Tenant" };
            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-test");
        await using var context = new MeepleAiDbContext(options, tenantContext);
        var logger = new TestLogger<AuditService>();
        var auditService = new AuditService(context, logger);

        // Log an access denied event
        await auditService.LogTenantAccessDeniedAsync(
            "tenant-test",
            "tenant-other",
            "user-123",
            "game",
            "game-456",
            "192.168.1.1",
            "TestAgent/1.0");

        // Verify audit log was created
        var auditLogs = await context.AuditLogs.ToListAsync();
        Assert.Single(auditLogs);

        var log = auditLogs[0];
        Assert.Equal("tenant-test", log.TenantId);
        Assert.Equal("user-123", log.UserId);
        Assert.Equal("ACCESS_DENIED", log.Action);
        Assert.Equal("game", log.Resource);
        Assert.Equal("game-456", log.ResourceId);
        Assert.Equal("Denied", log.Result);
        Assert.Contains("tenant-other", log.Details);
        Assert.Equal("192.168.1.1", log.IpAddress);
        Assert.Equal("TestAgent/1.0", log.UserAgent);
    }

    private class MockTenantContext : ITenantContext
    {
        public string? TenantId { get; }

        public MockTenantContext(string? tenantId)
        {
            TenantId = tenantId;
        }

        public string GetRequiredTenantId()
        {
            if (string.IsNullOrWhiteSpace(TenantId))
            {
                throw new UnauthorizedAccessException("No tenant context available");
            }
            return TenantId;
        }
    }

    private class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

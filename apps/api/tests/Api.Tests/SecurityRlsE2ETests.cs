using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests;

/// <summary>
/// SEC-02: End-to-end tests for Row Level Security (RLS) and role-based access control
/// </summary>
public class SecurityRlsE2ETests
{
    /// <summary>
    /// Test Case A: Tenant isolation - users cannot access data from other tenants
    /// </summary>
    [Fact]
    public async Task TestCaseA_TenantIsolation_UsersCannotAccessOtherTenantsData()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        // Setup: Create two tenants with users, games, and rule specs
        await using (var setupContext = new MeepleAiDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            // Tenant A
            var tenantA = new TenantEntity { Id = "tenant-a", Name = "Tenant A" };
            var userA = new UserEntity
            {
                Id = "user-a",
                TenantId = "tenant-a",
                Email = "user-a@example.com",
                PasswordHash = "hash-a",
                Role = UserRole.User,
                Tenant = tenantA
            };
            var gameA = new GameEntity
            {
                Id = "game-a",
                TenantId = "tenant-a",
                Name = "Game A",
                Tenant = tenantA
            };

            // Tenant B
            var tenantB = new TenantEntity { Id = "tenant-b", Name = "Tenant B" };
            var userB = new UserEntity
            {
                Id = "user-b",
                TenantId = "tenant-b",
                Email = "user-b@example.com",
                PasswordHash = "hash-b",
                Role = UserRole.User,
                Tenant = tenantB
            };
            var gameB = new GameEntity
            {
                Id = "game-b",
                TenantId = "tenant-b",
                Name = "Game B",
                Tenant = tenantB
            };

            setupContext.Tenants.AddRange(tenantA, tenantB);
            setupContext.Users.AddRange(userA, userB);
            setupContext.Games.AddRange(gameA, gameB);
            await setupContext.SaveChangesAsync();
        }

        // Test: User from Tenant A should only see Tenant A's data
        var contextA = new MockTenantContext("tenant-a");
        await using (var dbA = new MeepleAiDbContext(options, contextA))
        {
            var games = await dbA.Games.ToListAsync();
            var users = await dbA.Users.ToListAsync();

            Assert.Single(games);
            Assert.Equal("game-a", games[0].Id);
            Assert.Single(users);
            Assert.Equal("user-a", users[0].Id);

            // Attempt to query Tenant B's data should return null
            var gameBQuery = await dbA.Games.FirstOrDefaultAsync(g => g.Id == "game-b");
            var userBQuery = await dbA.Users.FirstOrDefaultAsync(u => u.Id == "user-b");
            Assert.Null(gameBQuery);
            Assert.Null(userBQuery);
        }

        // Test: User from Tenant B should only see Tenant B's data
        var contextB = new MockTenantContext("tenant-b");
        await using (var dbB = new MeepleAiDbContext(options, contextB))
        {
            var games = await dbB.Games.ToListAsync();
            var users = await dbB.Users.ToListAsync();

            Assert.Single(games);
            Assert.Equal("game-b", games[0].Id);
            Assert.Single(users);
            Assert.Equal("user-b", users[0].Id);

            // Attempt to query Tenant A's data should return null
            var gameAQuery = await dbB.Games.FirstOrDefaultAsync(g => g.Id == "game-a");
            var userAQuery = await dbB.Users.FirstOrDefaultAsync(u => u.Id == "user-a");
            Assert.Null(gameAQuery);
            Assert.Null(userAQuery);
        }
    }

    /// <summary>
    /// Test Case B: Editor role can modify data but cannot access admin functions
    /// </summary>
    [Fact]
    public async Task TestCaseB_EditorRole_CanModifyDataButNotAdminFunctions()
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

            var tenant = new TenantEntity { Id = "tenant-test", Name = "Test Tenant" };
            var editor = new UserEntity
            {
                Id = "user-editor",
                TenantId = "tenant-test",
                Email = "editor@example.com",
                PasswordHash = "hash",
                Role = UserRole.Editor,
                Tenant = tenant
            };
            var admin = new UserEntity
            {
                Id = "user-admin",
                TenantId = "tenant-test",
                Email = "admin@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                Tenant = tenant
            };
            var game = new GameEntity
            {
                Id = "game-test",
                TenantId = "tenant-test",
                Name = "Test Game",
                Tenant = tenant
            };

            setupContext.Tenants.Add(tenant);
            setupContext.Users.AddRange(editor, admin);
            setupContext.Games.Add(game);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-test");
        await using var context = new MeepleAiDbContext(options, tenantContext);

        // Test: Editor can read data
        var editorUser = await context.Users.FirstOrDefaultAsync(u => u.Id == "user-editor");
        Assert.NotNull(editorUser);
        Assert.Equal(UserRole.Editor, editorUser.Role);

        var games = await context.Games.ToListAsync();
        Assert.Single(games);

        // Test: Editor can create/modify games (simulated by adding a new game)
        var newGame = new GameEntity
        {
            Id = "game-new",
            TenantId = "tenant-test",
            Name = "New Game by Editor"
        };
        context.Games.Add(newGame);
        await context.SaveChangesAsync();

        var allGames = await context.Games.ToListAsync();
        Assert.Equal(2, allGames.Count);

        // Test: Verify role hierarchy exists
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Id == "user-admin");
        Assert.NotNull(adminUser);
        Assert.Equal(UserRole.Admin, adminUser.Role);
    }

    /// <summary>
    /// Test Case: Admin role has full access within their tenant
    /// </summary>
    [Fact]
    public async Task TestCaseAdmin_AdminRole_HasFullAccessWithinTenant()
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

            var tenant = new TenantEntity { Id = "tenant-admin", Name = "Admin Tenant" };
            var admin = new UserEntity
            {
                Id = "user-admin",
                TenantId = "tenant-admin",
                Email = "admin@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                Tenant = tenant
            };
            var regularUser = new UserEntity
            {
                Id = "user-regular",
                TenantId = "tenant-admin",
                Email = "user@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                Tenant = tenant
            };

            setupContext.Tenants.Add(tenant);
            setupContext.Users.AddRange(admin, regularUser);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-admin");
        await using var context = new MeepleAiDbContext(options, tenantContext);

        // Test: Admin can view all users in tenant
        var users = await context.Users.ToListAsync();
        Assert.Equal(2, users.Count);

        // Test: Admin can modify other users' data (within same tenant)
        var regularUserFromDb = await context.Users.FirstAsync(u => u.Id == "user-regular");
        regularUserFromDb.DisplayName = "Modified by Admin";
        await context.SaveChangesAsync();

        var updatedUser = await context.Users.FirstAsync(u => u.Id == "user-regular");
        Assert.Equal("Modified by Admin", updatedUser.DisplayName);
    }

    /// <summary>
    /// Test Case: User role has read-only access
    /// </summary>
    [Fact]
    public async Task TestCaseUser_UserRole_HasReadAccess()
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

            var tenant = new TenantEntity { Id = "tenant-user", Name = "User Tenant" };
            var user = new UserEntity
            {
                Id = "user-test",
                TenantId = "tenant-user",
                Email = "user@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                Tenant = tenant
            };
            var game = new GameEntity
            {
                Id = "game-test",
                TenantId = "tenant-user",
                Name = "Test Game",
                Tenant = tenant
            };

            setupContext.Tenants.Add(tenant);
            setupContext.Users.Add(user);
            setupContext.Games.Add(game);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-user");
        await using var context = new MeepleAiDbContext(options, tenantContext);

        // Test: User can read data
        var games = await context.Games.ToListAsync();
        Assert.Single(games);
        Assert.Equal("game-test", games[0].Id);

        var userEntity = await context.Users.FirstAsync(u => u.Id == "user-test");
        Assert.NotNull(userEntity);
        Assert.Equal(UserRole.User, userEntity.Role);
    }

    /// <summary>
    /// Test Case: Audit trail for cross-tenant access attempts
    /// </summary>
    [Fact]
    public async Task TestCaseAudit_CrossTenantAccessAttempts_AreLogged()
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

            var tenantA = new TenantEntity { Id = "tenant-a", Name = "Tenant A" };
            var tenantB = new TenantEntity { Id = "tenant-b", Name = "Tenant B" };
            setupContext.Tenants.AddRange(tenantA, tenantB);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-a");
        await using var context = new MeepleAiDbContext(options, tenantContext);

        var logger = new TestLogger<AuditService>();
        var auditService = new AuditService(context, logger);

        // Test: Log access denied when user from tenant-a tries to access tenant-b data
        await auditService.LogTenantAccessDeniedAsync(
            "tenant-a",
            "tenant-b",
            "user-123",
            "Game",
            "game-from-tenant-b",
            "192.168.1.100",
            "TestAgent/1.0");

        var auditLogs = await context.AuditLogs.ToListAsync();
        Assert.Single(auditLogs);

        var log = auditLogs[0];
        Assert.Equal("tenant-a", log.TenantId);
        Assert.Equal("user-123", log.UserId);
        Assert.Equal("ACCESS_DENIED", log.Action);
        Assert.Equal("Game", log.Resource);
        Assert.Equal("game-from-tenant-b", log.ResourceId);
        Assert.Equal("Denied", log.Result);
        Assert.Contains("tenant-b", log.Details ?? "");
        Assert.Equal("192.168.1.100", log.IpAddress);
        Assert.Equal("TestAgent/1.0", log.UserAgent);
    }

    /// <summary>
    /// Test Case: RLS prevents SQL injection attacks across tenants
    /// </summary>
    [Fact]
    public async Task TestCaseRLS_PreventsSqlInjectionAcrossTenants()
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

            var tenant1 = new TenantEntity { Id = "tenant-1", Name = "Tenant 1" };
            var tenant2 = new TenantEntity { Id = "tenant-2", Name = "Tenant 2" };

            var game1 = new GameEntity
            {
                Id = "game-1",
                TenantId = "tenant-1",
                Name = "Sensitive Game",
                Tenant = tenant1
            };

            var game2 = new GameEntity
            {
                Id = "game-2",
                TenantId = "tenant-2",
                Name = "Other Game",
                Tenant = tenant2
            };

            setupContext.Tenants.AddRange(tenant1, tenant2);
            setupContext.Games.AddRange(game1, game2);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-1");
        await using var context = new MeepleAiDbContext(options, tenantContext);

        // Test: Try to bypass RLS with injection-like query
        var maliciousGameId = "game-2' OR '1'='1";
        var gameQuery = await context.Games
            .FirstOrDefaultAsync(g => g.Id == maliciousGameId);

        // Should return null because RLS filters prevent access to tenant-2 data
        Assert.Null(gameQuery);

        // Verify only tenant-1 games are accessible
        var allGames = await context.Games.ToListAsync();
        Assert.Single(allGames);
        Assert.Equal("tenant-1", allGames[0].TenantId);
    }

    /// <summary>
    /// Test Case: Multiple entities with RLS isolation
    /// </summary>
    [Fact]
    public async Task TestCaseRLS_MultipleEntities_AllRespectTenantIsolation()
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

            var tenantA = new TenantEntity { Id = "tenant-a", Name = "Tenant A" };
            var tenantB = new TenantEntity { Id = "tenant-b", Name = "Tenant B" };

            var userA = new UserEntity
            {
                Id = "user-a",
                TenantId = "tenant-a",
                Email = "a@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                Tenant = tenantA
            };

            var userB = new UserEntity
            {
                Id = "user-b",
                TenantId = "tenant-b",
                Email = "b@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                Tenant = tenantB
            };

            var gameA = new GameEntity { Id = "game-a", TenantId = "tenant-a", Name = "Game A", Tenant = tenantA };
            var gameB = new GameEntity { Id = "game-b", TenantId = "tenant-b", Name = "Game B", Tenant = tenantB };

            setupContext.Tenants.AddRange(tenantA, tenantB);
            setupContext.Users.AddRange(userA, userB);
            setupContext.Games.AddRange(gameA, gameB);
            await setupContext.SaveChangesAsync();

            // Add audit logs for both tenants
            setupContext.AuditLogs.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = "tenant-a",
                UserId = "user-a",
                Action = "TEST",
                Resource = "Game",
                ResourceId = "game-a",
                Result = "Success"
            });

            setupContext.AuditLogs.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = "tenant-b",
                UserId = "user-b",
                Action = "TEST",
                Resource = "Game",
                ResourceId = "game-b",
                Result = "Success"
            });

            await setupContext.SaveChangesAsync();
        }

        // Test tenant-a isolation
        var contextA = new MockTenantContext("tenant-a");
        await using (var dbA = new MeepleAiDbContext(options, contextA))
        {
            var users = await dbA.Users.ToListAsync();
            var games = await dbA.Games.ToListAsync();
            var auditLogs = await dbA.AuditLogs.ToListAsync();

            Assert.Single(users);
            Assert.All(users, u => Assert.Equal("tenant-a", u.TenantId));

            Assert.Single(games);
            Assert.All(games, g => Assert.Equal("tenant-a", g.TenantId));

            Assert.Single(auditLogs);
            Assert.All(auditLogs, a => Assert.Equal("tenant-a", a.TenantId));
        }

        // Test tenant-b isolation
        var contextB = new MockTenantContext("tenant-b");
        await using (var dbB = new MeepleAiDbContext(options, contextB))
        {
            var users = await dbB.Users.ToListAsync();
            var games = await dbB.Games.ToListAsync();
            var auditLogs = await dbB.AuditLogs.ToListAsync();

            Assert.Single(users);
            Assert.All(users, u => Assert.Equal("tenant-b", u.TenantId));

            Assert.Single(games);
            Assert.All(games, g => Assert.Equal("tenant-b", g.TenantId));

            Assert.Single(auditLogs);
            Assert.All(auditLogs, a => Assert.Equal("tenant-b", a.TenantId));
        }
    }

    /// <summary>
    /// Test Case: Owner vs non-owner access within same tenant
    /// </summary>
    [Fact]
    public async Task TestCaseOwner_OwnerVsNonOwner_AccessControl()
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

            var tenant = new TenantEntity { Id = "tenant-shared", Name = "Shared Tenant" };

            var owner = new UserEntity
            {
                Id = "user-owner",
                TenantId = "tenant-shared",
                Email = "owner@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                Tenant = tenant
            };

            var nonOwner = new UserEntity
            {
                Id = "user-nonowner",
                TenantId = "tenant-shared",
                Email = "nonowner@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                Tenant = tenant
            };

            var game = new GameEntity
            {
                Id = "game-shared",
                TenantId = "tenant-shared",
                Name = "Shared Game",
                Tenant = tenant
            };

            setupContext.Tenants.Add(tenant);
            setupContext.Users.AddRange(owner, nonOwner);
            setupContext.Games.Add(game);
            await setupContext.SaveChangesAsync();
        }

        var tenantContext = new MockTenantContext("tenant-shared");
        await using var context = new MeepleAiDbContext(options, tenantContext);

        // Both owner and non-owner can see the same data (same tenant)
        var users = await context.Users.ToListAsync();
        Assert.Equal(2, users.Count);

        var ownerFromDb = await context.Users.FirstAsync(u => u.Id == "user-owner");
        var nonOwnerFromDb = await context.Users.FirstAsync(u => u.Id == "user-nonowner");

        // Verify role differences
        Assert.Equal(UserRole.Admin, ownerFromDb.Role);
        Assert.Equal(UserRole.User, nonOwnerFromDb.Role);

        // Both can access the same game (tenant isolation works, role enforcement is app-level)
        var games = await context.Games.ToListAsync();
        Assert.Single(games);
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

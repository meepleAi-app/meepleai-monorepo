using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for <see cref="GetActiveImpersonationsQueryHandler"/> (SP5 Admin Security S2 — T6).
/// Verifies the active-impersonation predicate (ImpersonatedByUserId set, RevokedAt null,
/// ImpersonatedUntil &gt; now), the actor/subject email join, and the optional admin filter.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class GetActiveImpersonationsQueryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.Parse("2026-05-26T12:00:00Z"));

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetActiveImpersonationsQueryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_active_impersonations_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null) await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    [Fact]
    public async Task Handle_ReturnsOnlyActiveImpersonations_WithActorAndSubjectEmails()
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var alice = SeedUser(db, "alice@admin.test");   // actor
        var bob = SeedUser(db, "bob@user.test");        // subject (active)
        var carol = SeedUser(db, "carol@user.test");    // subject (expired)
        var dave = SeedUser(db, "dave@user.test");      // subject (revoked)
        var erin = SeedUser(db, "erin@user.test");      // regular login (not impersonation)
        await db.SaveChangesAsync(TestCancellationToken);

        // Active impersonation: alice → bob, until now+10min.
        SeedSession(db, userId: bob, impersonatedBy: alice, until: now.AddMinutes(10), revokedAt: null, expiresAt: now.AddMinutes(10));
        // Expired impersonation: alice → carol, until now-1min.
        SeedSession(db, userId: carol, impersonatedBy: alice, until: now.AddMinutes(-1), revokedAt: null, expiresAt: now.AddMinutes(-1));
        // Revoked impersonation: alice → dave.
        SeedSession(db, userId: dave, impersonatedBy: alice, until: now.AddMinutes(10), revokedAt: now.AddMinutes(-2), expiresAt: now.AddMinutes(10));
        // Regular login: erin (no impersonation).
        SeedSession(db, userId: erin, impersonatedBy: null, until: null, revokedAt: null, expiresAt: now.AddDays(30));
        await db.SaveChangesAsync(TestCancellationToken);

        var handler = new GetActiveImpersonationsQueryHandler(db, _timeProvider);

        // Act
        var result = await handler.Handle(new GetActiveImpersonationsQuery(), TestCancellationToken);

        // Assert — only the active alice→bob row.
        result.Should().HaveCount(1, "only the non-expired, non-revoked impersonation is active");
        var row = result[0];
        row.AdminUserId.Should().Be(alice);
        row.AdminEmail.Should().Be("alice@admin.test");
        row.TargetUserId.Should().Be(bob);
        row.TargetEmail.Should().Be("bob@user.test");
        row.ImpersonatedUntil.Should().Be(now.AddMinutes(10));
    }

    [Fact]
    public async Task Handle_FilterByAdmin_ReturnsOnlyThatAdminsImpersonations()
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var alice = SeedUser(db, "alice2@admin.test");
        var root = SeedUser(db, "root2@admin.test");
        var bob = SeedUser(db, "bob2@user.test");
        var carol = SeedUser(db, "carol2@user.test");
        await db.SaveChangesAsync(TestCancellationToken);

        SeedSession(db, userId: bob, impersonatedBy: alice, until: now.AddMinutes(10), revokedAt: null, expiresAt: now.AddMinutes(10));
        SeedSession(db, userId: carol, impersonatedBy: root, until: now.AddMinutes(10), revokedAt: null, expiresAt: now.AddMinutes(10));
        await db.SaveChangesAsync(TestCancellationToken);

        var handler = new GetActiveImpersonationsQueryHandler(db, _timeProvider);

        // Act — filter to alice only.
        var result = await handler.Handle(new GetActiveImpersonationsQuery(alice), TestCancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].AdminUserId.Should().Be(alice);
        result[0].TargetUserId.Should().Be(bob);
    }

    private static Guid SeedUser(MeepleAiDbContext db, string email)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = id,
            Email = email,
            DisplayName = email.Split('@')[0],
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        return id;
    }

    private static void SeedSession(
        MeepleAiDbContext db, Guid userId, Guid? impersonatedBy, DateTime? until, DateTime? revokedAt, DateTime expiresAt)
    {
        db.UserSessions.Add(new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            ImpersonatedByUserId = impersonatedBy,
            ImpersonatedUntil = until,
            User = null!,
        });
    }

    private static async Task ApplyMigrationsAsync(MeepleAiDbContext db)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
        }
    }
}

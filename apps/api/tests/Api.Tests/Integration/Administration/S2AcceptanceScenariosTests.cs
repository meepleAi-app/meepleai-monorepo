using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// SP5 Admin Security S2 — Task 8 acceptance scenarios (Gojko Adzic Given/When/Then). The DoD gate
/// for S2: all five must be green before merge. Mirrors S1AcceptanceScenariosTests.
///
/// Deviations (faithful, documented): scenarios that depend on the HTTP auth middleware (S2-3
/// auto-expiry 401, S2-4 subsequent-401) are exercised at the ValidateSessionQuery level — the
/// query is the detection point; the middleware's 401 + audit is a thin consumer of the flag
/// (unit-tested in T4 / ValidateSessionQueryImpersonationTests). Each scenario provisions its own
/// isolated database.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class S2AcceptanceScenariosTests
{
    private readonly SharedTestcontainersFixture _fixture;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public S2AcceptanceScenariosTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    // ─── Scenario S2-1 — Happy path start ────────────────────────────────────────────────────────
    [Fact(DisplayName = "Scenario S2-1 — ImpersonationStartCommand creates dual-principal session + Pending audit")]
    public async Task Scenario_1_HappyPathStart()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        // alice (superadmin) is the CALLER (not impersonating yet) — HttpContext carries her session
        // so the behavior reads adminUserId=alice and pairs it into impersonated_user_id (UserIdSource=ResourceId).
        var callerSession = new SessionStatusDto(
            IsValid: true,
            Principal: new Principal(Subject: UserDtoFor(alice, "alice@s2-1.test", "superadmin"), Actor: null),
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            LastSeenAt: DateTime.UtcNow,
            SessionId: Guid.NewGuid());

        await using var ctx = await SetupAsync(callerSession);

        await SeedUserAsync(ctx.Db, "alice@s2-1.test", "superadmin", alice);
        await SeedUserAsync(ctx.Db, "bob@s2-1.test", "user", bob);

        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        var result = await mediator.Send(
            new ImpersonationStartCommand(bob, alice, "Investigating reported library bug", DurationMinutes: 15),
            TestCancellationToken);

        // Session created with dual-principal fields.
        ctx.Db.ChangeTracker.Clear();
        var sessionRow = await ctx.Db.UserSessions.AsNoTracking()
            .SingleAsync(s => s.Id == result.SessionId, TestCancellationToken);
        sessionRow.UserId.Should().Be(bob, "subject = impersonated user");
        sessionRow.ImpersonatedByUserId.Should().Be(alice, "actor = the superadmin");
        sessionRow.ImpersonatedUntil.Should().NotBeNull();

        // Pending outbox row → drain → audit_logs with subject/actor attribution.
        var processor = ctx.Sp.GetRequiredService<AuditOutboxProcessor>();
        await processor.RunOnceAsync(100, TestCancellationToken);
        ctx.Db.ChangeTracker.Clear();

        var audit = await ctx.Db.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.Action == "ImpersonationStarted", TestCancellationToken);
        audit.UserId.Should().Be(bob, "D-S2-3: user_id = target (subject)");
        audit.ImpersonatedUserId.Should().Be(alice, "D-S2-3: impersonated_user_id = actor (superadmin)");
        audit.Resource.Should().Be("Session");
    }

    // ─── Scenario S2-2 — Auditable destructive command during impersonate ─────────────────────────
    [Fact(DisplayName = "Scenario S2-2 — destructive command during impersonate attributes subject + actor")]
    public async Task Scenario_2_DestructiveCommandDuringImpersonate()
    {
        var alice = Guid.NewGuid();   // superadmin actor
        var bob = Guid.NewGuid();     // impersonated subject

        // HttpContext carrying an active impersonation principal (subject=bob, actor=alice).
        var impersonationSession = new SessionStatusDto(
            IsValid: true,
            Principal: new Principal(
                Subject: UserDtoFor(bob, "bob@s2-2.test", "user"),
                Actor: UserDtoFor(alice, "alice@s2-2.test", "superadmin")),
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            LastSeenAt: DateTime.UtcNow,
            SessionId: Guid.NewGuid());

        await using var ctx = await SetupAsync(impersonationSession);

        await SeedUserAsync(ctx.Db, "alice@s2-2.test", "superadmin", alice);
        await SeedUserAsync(ctx.Db, "bob@s2-2.test", "user", bob);
        var carol = await SeedUserAsync(ctx.Db, "carol@s2-2.test", "user");

        // bob (impersonated by alice) deletes carol. DeleteUserCommand is [AtomicAudit].
        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        await mediator.Send(new DeleteUserCommand(carol.ToString(), bob.ToString()), TestCancellationToken);

        ctx.Db.ChangeTracker.Clear();
        (await ctx.Db.Users.AsNoTracking().AnyAsync(u => u.Id == carol, TestCancellationToken))
            .Should().BeFalse("the delete still commits (atomicity preserved under impersonation)");

        var processor = ctx.Sp.GetRequiredService<AuditOutboxProcessor>();
        await processor.RunOnceAsync(100, TestCancellationToken);
        ctx.Db.ChangeTracker.Clear();

        var audit = await ctx.Db.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.Action == "UserDelete", TestCancellationToken);
        audit.UserId.Should().Be(bob, "D-S2-3: user_id = the subject (impersonated user acting)");
        audit.ImpersonatedUserId.Should().Be(alice, "D-S2-3: impersonated_user_id = the acting admin");
    }

    // ─── Scenario S2-3 — Auto-expiry mid-flight ───────────────────────────────────────────────────
    [Fact(DisplayName = "Scenario S2-3 — expired impersonation flagged auto-ended (middleware → 401 + audit)")]
    public async Task Scenario_3_AutoExpiry()
    {
        await using var ctx = await SetupAsync();

        var alice = await SeedUserAsync(ctx.Db, "alice@s2-3.test", "superadmin");
        var bob = await SeedUserAsync(ctx.Db, "bob@s2-3.test", "user");

        // Seed an impersonation session whose window elapsed 1 minute ago.
        var token = Api.BoundedContexts.Authentication.Domain.ValueObjects.SessionToken.Generate();
        var past = DateTime.UtcNow.AddMinutes(-1);
        ctx.Db.UserSessions.Add(new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = bob,
            TokenHash = token.ComputeHash(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-16),
            ExpiresAt = past,
            ImpersonatedByUserId = alice,
            ImpersonatedUntil = past,
            User = null!,
        });
        await ctx.Db.SaveChangesAsync(TestCancellationToken);
        ctx.Db.ChangeTracker.Clear();

        // The auth middleware calls ValidateSessionQuery; the expired impersonation surfaces the
        // auto-ended signal that drives the explicit 401 + ImpersonationAutoEnded audit (T4).
        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        var status = await mediator.Send(new ValidateSessionQuery(token.Value), TestCancellationToken);

        status.IsValid.Should().BeFalse("the impersonation window elapsed");
        status.WasImpersonationAutoEnded.Should().BeTrue("middleware must emit 401 + ImpersonationAutoEnded");
        status.ImpersonationSubjectUserId.Should().Be(bob);
        status.ImpersonationActorUserId.Should().Be(alice);
    }

    // ─── Scenario S2-4 — Superadmin revoke kills in-flight session ────────────────────────────────
    [Fact(DisplayName = "Scenario S2-4 — superadmin revoke marks RevokedAt, audits, invalidates session")]
    public async Task Scenario_4_SuperadminRevoke()
    {
        await using var ctx = await SetupAsync();

        var root = await SeedUserAsync(ctx.Db, "root@s2-4.test", "superadmin");
        var alice = await SeedUserAsync(ctx.Db, "alice@s2-4.test", "superadmin");  // impersonating admin
        var bob = await SeedUserAsync(ctx.Db, "bob@s2-4.test", "user");

        // Active impersonation alice → bob.
        var token = Api.BoundedContexts.Authentication.Domain.ValueObjects.SessionToken.Generate();
        var sessionId = Guid.NewGuid();
        ctx.Db.UserSessions.Add(new UserSessionEntity
        {
            Id = sessionId,
            UserId = bob,
            TokenHash = token.ComputeHash(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            ImpersonatedByUserId = alice,
            ImpersonatedUntil = DateTime.UtcNow.AddMinutes(15),
            User = null!,
        });
        await ctx.Db.SaveChangesAsync(TestCancellationToken);
        ctx.Db.ChangeTracker.Clear();

        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        var revoked = await mediator.Send(new RevokeImpersonationCommand(sessionId, root), TestCancellationToken);
        revoked.Should().BeTrue();

        ctx.Db.ChangeTracker.Clear();
        var sessionRow = await ctx.Db.UserSessions.AsNoTracking()
            .SingleAsync(s => s.Id == sessionId, TestCancellationToken);
        sessionRow.RevokedAt.Should().NotBeNull("revoke must mark RevokedAt");

        // Audit attribution: user_id = alice (subject of revoke), impersonated_user_id = root (actor).
        var audit = await ctx.Db.AuditOutbox.AsNoTracking().ToListAsync(TestCancellationToken);
        var payload = audit
            .Select(r => System.Text.Json.JsonSerializer.Deserialize<AuditOutboxPayload>(r.PayloadJson)!)
            .Single(p => p.Action == "ImpersonationRevoked");
        payload.UserId.Should().Be(alice.ToString(), "Scenario S2-4: user_id = the impersonating admin");
        payload.ImpersonatedUserId.Should().Be(root, "Scenario S2-4: impersonated_user_id = the superadmin");

        // Subsequent request equivalence: the session is now invalid (middleware → 401).
        var status = await mediator.Send(new ValidateSessionQuery(token.Value), TestCancellationToken);
        status.IsValid.Should().BeFalse("a revoked session must not validate (subsequent 401)");
    }

    // ─── Scenario S2-5 — Cannot impersonate a superadmin ──────────────────────────────────────────
    [Fact(DisplayName = "Scenario S2-5 — impersonating a superadmin is forbidden, no session created")]
    public async Task Scenario_5_CannotImpersonateSuperAdmin()
    {
        await using var ctx = await SetupAsync();

        var alice = await SeedUserAsync(ctx.Db, "alice@s2-5.test", "superadmin");
        var trent = await SeedUserAsync(ctx.Db, "trent@s2-5.test", "superadmin");

        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        var act = () => mediator.Send(
            new ImpersonationStartCommand(trent, alice, "attempt to impersonate a peer superadmin"),
            TestCancellationToken);

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ForbiddenException>()
            .WithMessage("*Cannot impersonate admin or superadmin*");

        ctx.Db.ChangeTracker.Clear();
        (await ctx.Db.UserSessions.AsNoTracking().AnyAsync(s => s.UserId == trent, TestCancellationToken))
            .Should().BeFalse("no impersonation session must be created for a forbidden target");
    }

    // ═══ Helpers ══════════════════════════════════════════════════════════════════════════════════

    private sealed class TestContextEnv : IAsyncDisposable
    {
        public required ServiceProvider Sp { get; init; }
        public required MeepleAiDbContext Db { get; init; }
        public required string DatabaseName { get; init; }
        public required SharedTestcontainersFixture Fixture { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Sp.DisposeAsync();
            try { await Fixture.DropIsolatedDatabaseAsync(DatabaseName); }
            catch { /* best-effort */ }
        }
    }

    private async Task<TestContextEnv> SetupAsync(SessionStatusDto? impersonationContext = null)
    {
        var dbName = $"test_s2_acceptance_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddSingleton<IAuditOutboxHealthTracker, AuditOutboxHealthTracker>();
        services.AddSingleton<AuditOutboxProcessor>();

        // IHttpContextAccessor: for S2-2 it carries an impersonation SessionStatusDto so the
        // AuditLoggingBehavior attributes subject+actor; otherwise a null HttpContext (handled gracefully).
        var accessor = new Mock<IHttpContextAccessor>();
        if (impersonationContext is not null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[nameof(SessionStatusDto)] = impersonationContext;
            accessor.Setup(a => a.HttpContext).Returns(httpContext);
        }
        services.AddSingleton(accessor.Object);

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(db);

        return new TestContextEnv { Sp = sp, Db = db, DatabaseName = dbName, Fixture = _fixture };
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db, string email, string role, Guid? id = null)
    {
        var userId = id ?? Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = email.Split('@')[0],
            Role = role,
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();
        return userId;
    }

    private static UserDto UserDtoFor(Guid id, string email, string role)
        => new(
            Id: id,
            Email: email,
            DisplayName: email.Split('@')[0],
            Role: role,
            Tier: "free",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null,
            Level: 1,
            ExperiencePoints: 0);

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

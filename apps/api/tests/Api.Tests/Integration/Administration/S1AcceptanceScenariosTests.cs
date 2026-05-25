using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.BoundedContexts.Administration.Infrastructure.Health;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// SP5 Admin Security S1 — Task 5 acceptance scenarios (Gojko Adzic Given/When/Then style).
///
/// Five end-to-end integration tests that constitute the DoD gate for S1. All five MUST be
/// green before S1 is merged.
///
/// Deviations from the supplement spec (faithful adaptation, semantics preserved):
///   • <c>UserEntity</c> is HARD-delete by design (no <c>IsDeleted</c> column / no query filter).
///     Scenario 1 asserts row absence rather than <c>bob.IsDeleted == true</c>.
///   • <c>UserEntity</c> has no <c>Games</c> navigation. Scenario 5 substitutes
///     <c>BackupCodes</c> as the bulk navigation collection that triggers truncation —
///     accessed via <c>ChangeUserRoleCommand</c> whose handler issues <c>UpdateAsync</c>
///     (which <c>.Include(u =&gt; u.BackupCodes).AsTracking()</c> — see UserRepository:144).
///   • The <c>AdministrationFixture.CreateAsync()</c> helper from the spec does not exist
///     in this codebase; we follow the established <c>SharedTestcontainersFixture</c> +
///     <c>IntegrationServiceCollectionBuilder.CreateBase</c> pattern from
///     <c>AtomicAuditIntegrationTests</c>.
///
/// Each scenario provisions its own isolated database to avoid cross-test state leaks.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class S1AcceptanceScenariosTests
{
    private readonly SharedTestcontainersFixture _fixture;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public S1AcceptanceScenariosTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 1 — Audit di un delete user atomico
    //   GIVEN an admin and a target user
    //   WHEN  the admin issues DeleteUserCommand against the target
    //   THEN  the target row is hard-deleted
    //   AND   exactly one Pending audit_outbox row is enqueued with action="UserDelete"
    //   AND   the outbox payload contains the target user's email (BeforeJson snapshot)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 1 — DeleteUserCommand: atomic mutation + Pending outbox row")]
    public async Task Scenario_1_DeleteUserAtomic_EnqueuesPendingOutboxRow()
    {
        await using var ctx = await SetupAsync();

        // Given: 2 admins (so the "last admin" guard passes if target were admin) + 1 regular target.
        var requestingAdminId = await SeedUserAsync(ctx.Db, "alice@meeple.app", "superadmin");
        var anotherAdminId    = await SeedUserAsync(ctx.Db, "second@meeple.app", "admin");
        var targetUserId      = await SeedUserAsync(ctx.Db, "bob@example.com",   "user");

        // When
        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        var act = () => mediator.Send(
            new DeleteUserCommand(targetUserId.ToString(), requestingAdminId.ToString()),
            TestCancellationToken);
        await act.Should().NotThrowAsync(because: "Scenario 1 happy-path");

        // Then 1: target hard-deleted
        ctx.Db.ChangeTracker.Clear();
        (await ctx.Db.Users.AsNoTracking().AnyAsync(u => u.Id == targetUserId, TestCancellationToken))
            .Should().BeFalse(because: "DeleteUserCommand hard-deletes the row");

        // Then 2: one Pending outbox row, with the right action + payload contents
        var outboxRows = await ctx.Db.AuditOutbox.AsNoTracking().ToListAsync(TestCancellationToken);
        outboxRows.Should().HaveCount(1,
            because: "[AtomicAudit] DeleteUserCommand enqueues exactly one outbox row");
        outboxRows[0].Status.Should().Be(OutboxStatus.Pending,
            because: "the T4 processor has not run yet");

        // Deserialize the payload via the same DTO the AuditService uses so the assertion
        // is robust to whitespace / property-naming choices in the underlying JsonSerializerOptions.
        var payload = JsonSerializer.Deserialize<AuditOutboxPayload>(outboxRows[0].PayloadJson);
        payload.Should().NotBeNull();
        payload!.Action.Should().Be("UserDelete",
            because: "the AuditableAction attribute on DeleteUserCommand declares action=UserDelete");
        payload.Resource.Should().Be("User");
        payload.Result.Should().Be("Success");
        payload.Snapshots.Should().NotBeEmpty();
        payload.Snapshots.Should().Contain(s => s.BeforeJson != null && s.BeforeJson.Contains("bob@example.com"),
            because: "the delete operation captures the target user's email in the before-state snapshot");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 2 — Audit row promoted by processor
    //   GIVEN Scenario 1 final state (1 Pending outbox row, 0 audit_logs)
    //   WHEN  AuditOutboxProcessor.RunOnceAsync(100) runs
    //   THEN  processed = 1
    //   AND   audit_logs has 1 row with Id == outbox.Id (T4b idempotency key contract)
    //   AND   outbox row transitions Pending → Sent
    //   AND   audit_log.BeforeJson is populated AND AfterJson is null (delete semantics)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 2 — Processor materializes UserDelete audit + outbox.Id == audit_logs.Id")]
    public async Task Scenario_2_AuditRowPromotedByProcessor()
    {
        await using var ctx = await SetupAsync();

        var requestingAdminId = await SeedUserAsync(ctx.Db, "alice@s2.test", "superadmin");
        var anotherAdminId    = await SeedUserAsync(ctx.Db, "second@s2.test", "admin");
        var targetUserId      = await SeedUserAsync(ctx.Db, "bob@s2.test",    "user");

        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        await mediator.Send(
            new DeleteUserCommand(targetUserId.ToString(), requestingAdminId.ToString()),
            TestCancellationToken);

        // When
        var processor = ctx.Sp.GetRequiredService<AuditOutboxProcessor>();
        var processed = await processor.RunOnceAsync(100, TestCancellationToken);

        // Then
        processed.Should().Be(1, because: "exactly one Pending row existed");

        ctx.Db.ChangeTracker.Clear();
        var auditLog  = await ctx.Db.AuditLogs.AsNoTracking().SingleAsync(TestCancellationToken);
        var outboxRow = await ctx.Db.AuditOutbox.AsNoTracking().SingleAsync(TestCancellationToken);

        auditLog.Id.Should().Be(outboxRow.Id,
            because: "T4b contract: audit_logs.Id == outbox.Id so retries are idempotent");
        outboxRow.Status.Should().Be(OutboxStatus.Sent,
            because: "drain successful → Pending must transition to Sent");
        auditLog.Action.Should().Be("UserDelete");
        auditLog.Resource.Should().Be("User");
        auditLog.Result.Should().Be("Success");

        auditLog.BeforeJson.Should().NotBeNullOrEmpty(
            because: "a delete operation captures the pre-state in BeforeJson");
        auditLog.AfterJson.Should().BeNull(
            because: "a delete operation has no post-state — AfterJson must be null");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 3 — Crash recovery: no duplicates on processor restart
    //   GIVEN 5 Pending rows successfully drained once → 5 audit_logs rows
    //   WHEN  2 outbox rows are re-flagged Pending (simulating a recovery scenario)
    //   AND   the processor runs again
    //   THEN  processed = 2, but audit_logs still holds EXACTLY 5 rows (no duplicates)
    //   AND   all 5 outbox rows end as Sent
    //
    // Mirrors AuditOutboxIdempotencyTests.Processor_DoesNotDuplicate_OnRetryAfterPartialBatch
    // with the DisplayName aligned to the three-amigos numbering for DoD traceability.
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 3 — Crash recovery: no duplicate audit_logs on processor restart")]
    public async Task Scenario_3_CrashRecovery_NoDuplicates()
    {
        await using var ctx = await SetupAsync();

        var now = DateTimeOffset.UtcNow;
        var ids = new List<Guid>(capacity: 5);
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            ctx.Db.AuditOutbox.Add(AuditOutboxEntity.CreatePending(
                id,
                JsonSerializer.Serialize(BuildPayload($"Crash.{i}", now.AddSeconds(i))),
                now.AddSeconds(i)));
        }
        await ctx.Db.SaveChangesAsync(TestCancellationToken);

        var processor = ctx.Sp.GetRequiredService<AuditOutboxProcessor>();
        var processed1 = await processor.RunOnceAsync(100, TestCancellationToken);

        processed1.Should().Be(5);
        ctx.Db.ChangeTracker.Clear();
        (await ctx.Db.AuditLogs.AsNoTracking().CountAsync(TestCancellationToken)).Should().Be(5);

        // Simulate recovery: re-flag the first 2 outbox rows back to Pending.
        var firstTwoIds = ids.Take(2).ToList();
        var firstTwo = await ctx.Db.AuditOutbox
            .Where(r => firstTwoIds.Contains(r.Id))
            .ToListAsync(TestCancellationToken);
        foreach (var row in firstTwo) row.MarkPendingForTest();
        await ctx.Db.SaveChangesAsync(TestCancellationToken);

        var processed2 = await processor.RunOnceAsync(100, TestCancellationToken);

        processed2.Should().Be(2);
        ctx.Db.ChangeTracker.Clear();
        (await ctx.Db.AuditLogs.AsNoTracking().CountAsync(TestCancellationToken))
            .Should().Be(5, because: "T4b idempotency: audit_logs must NOT grow on retry");
        (await ctx.Db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Sent, TestCancellationToken))
            .Should().Be(5, because: "all rows end Sent after the recovery drain");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 4 — Best-effort: non-destructive handler audit failure is non-fatal
    //   GIVEN a target user AND an AuditService whose EnqueueAuditAsync unconditionally throws
    //   WHEN  the admin issues ChangeUserRoleCommand (best-effort path, no [AtomicAudit])
    //   THEN  the role change persists (mutation committed despite audit enqueue failure)
    //   AND   no exception propagates to the caller
    //   AND   audit_outbox remains empty (the throwing stub never wrote a row)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 4 — Best-effort: ChangeUserRole succeeds even when audit enqueue fails")]
    public async Task Scenario_4_BestEffort_RoleChangePersistsWhenAuditFails()
    {
        // Inject a ThrowingBestEffortAuditService for THIS test only.
        await using var ctx = await SetupAsync(auditServiceOverride: new ThrowingBestEffortAuditService());

        var targetUserId = await SeedUserAsync(ctx.Db, "promote.me@s4.test", "user");

        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        var command  = new ChangeUserRoleCommand(
            UserId:    targetUserId.ToString(),
            NewRole:   "Editor",
            Reason:    "Scenario 4 acceptance test",
            AdminRole: "superadmin");

        // When + Then: must NOT throw despite the audit stub being broken.
        var act = () => mediator.Send(command, TestCancellationToken);
        await act.Should().NotThrowAsync(
            because: "best-effort path swallows audit failures so business operations never break");

        // Then: role mutation committed
        ctx.Db.ChangeTracker.Clear();
        var user = await ctx.Db.Users.AsNoTracking()
            .SingleAsync(u => u.Id == targetUserId, TestCancellationToken);
        // Role value object normalizes to lowercase (see SharedKernel/Domain/ValueObjects/Role.cs:25).
        user.Role.Should().Be("editor",
            because: "the role change must persist even when audit enqueue throws");

        // Then: no outbox row (the throwing stub never enqueued)
        (await ctx.Db.AuditOutbox.AsNoTracking().CountAsync(TestCancellationToken))
            .Should().Be(0,
                because: "ThrowingBestEffortAuditService.EnqueueAuditAsync throws before writing the row");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 5 — Payload truncation guard
    //   GIVEN a target user with 60 BackupCodes loaded+tracked in the request DbContext
    //   WHEN  the admin issues DeleteUserCommand and the processor drains the outbox row
    //   THEN  the materialized audit_log.BeforeJson contains the "_truncated" marker
    //   AND   the truncated list names "BackupCodes"
    //   AND   BeforeJson size stays ≤ 256_000 bytes (PayloadTruncator.MaxPayloadBytes)
    //   AND   the user is still hard-deleted (mutation independent of audit truncation)
    //
    // Why DeleteUserCommand + pre-load: the truncation logic lives in the
    // AuditingSaveChangesInterceptor, which only sees navigation children that are LOADED and
    // tracked at SaveChanges time. We deliberately pre-load the target's 60 BackupCodes into the
    // shared request DbContext so DeleteUserCommandHandler.DeleteAsync (which calls FindAsync —
    // returning the already-tracked instance via identity resolution) carries the populated
    // collection into the snapshot. This forces the >50-item precondition that exercises
    // PayloadTruncator end-to-end. (ChangeUserRoleCommand is unusable here: UserRepository.UpdateAsync
    // Clear()s + rebuilds BackupCodes from the domain aggregate — UserRepository:205 — wiping them
    // before SaveChanges. The Interests scalar is also unusable: its jsonb mapping needs
    // EnableDynamicJson, not configured in the minimal test data source.)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 5 — Payload truncation: 60 BackupCodes triggers _truncated marker")]
    public async Task Scenario_5_PayloadTruncationGuard()
    {
        await using var ctx = await SetupAsync();

        var requestingAdminId = await SeedUserAsync(ctx.Db, "admin@s5.test", "superadmin");
        var targetUserId      = await SeedUserAsync(ctx.Db, "heavy@s5.test", "user");

        // Seed 60 backup codes for the target — above the trim threshold (50).
        for (var i = 0; i < 60; i++)
        {
            ctx.Db.UserBackupCodes.Add(new UserBackupCodeEntity
            {
                Id        = Guid.NewGuid(),
                UserId    = targetUserId,
                CodeHash  = $"hash-{i:D3}-{Guid.NewGuid():N}",
                IsUsed    = false,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await ctx.Db.SaveChangesAsync(TestCancellationToken);
        ctx.Db.ChangeTracker.Clear();

        // Pre-load the target user WITH its 60 backup codes into the shared request DbContext.
        // DeleteUserCommandHandler.DeleteAsync calls DbContext.Users.FindAsync(id) which returns
        // this already-tracked instance (with BackupCodes populated) via identity resolution, so
        // the interceptor's delete snapshot captures all 60 child PKs → truncation fires.
        // NOTE: deliberately NOT clearing the change tracker after this load.
        var tracked = await ctx.Db.Users
            .Include(u => u.BackupCodes)
            .AsTracking()
            .FirstAsync(u => u.Id == targetUserId, TestCancellationToken);
        tracked.BackupCodes.Should().HaveCount(60,
            because: "the pre-load must populate the navigation before the command runs");

        // When
        var mediator = ctx.Sp.GetRequiredService<IMediator>();
        await mediator.Send(
            new DeleteUserCommand(targetUserId.ToString(), requestingAdminId.ToString()),
            TestCancellationToken);

        var processor = ctx.Sp.GetRequiredService<AuditOutboxProcessor>();
        var processed = await processor.RunOnceAsync(100, TestCancellationToken);

        // Then
        processed.Should().Be(1);
        ctx.Db.ChangeTracker.Clear();

        var auditLog = await ctx.Db.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.Action == "UserDelete", TestCancellationToken);
        auditLog.BeforeJson.Should().NotBeNullOrEmpty(
            because: "a delete operation captures the pre-state snapshot");
        auditLog.BeforeJson!.Should().Contain("_truncated",
            because: "60 BackupCodes exceeds the trim threshold (50) → marker emitted");
        auditLog.BeforeJson!.Should().Contain("BackupCodes",
            because: "the truncated-fields list must name the collection that was trimmed");
        Encoding.UTF8.GetByteCount(auditLog.BeforeJson!).Should().BeLessThanOrEqualTo(256_000,
            because: "PayloadTruncator must keep the serialized snapshot within the byte budget");

        // Mutation still committed (hard delete).
        (await ctx.Db.Users.AsNoTracking().AnyAsync(u => u.Id == targetUserId, TestCancellationToken))
            .Should().BeFalse(because: "the delete must commit regardless of snapshot truncation");
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Per-scenario test context. Each Fact provisions a fresh isolated database so the five
    /// scenarios are mutually independent (no shared audit_outbox / audit_logs state).
    /// </summary>
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
            catch { /* best-effort cleanup */ }
        }
    }

    private async Task<TestContextEnv> SetupAsync(AuditService? auditServiceOverride = null)
    {
        var dbName = $"test_s1_acceptance_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Pipeline behavior + handler dependencies the acceptance scenarios touch.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        services.AddScoped<IUserRepository, UserRepository>();

        // The behavior reads from IHttpContextAccessor; a null HttpContext is handled gracefully
        // (admin user id / ip / user-agent become null in the audit payload — acceptable for
        // acceptance tests since we assert on action/resource/email, not on the actor side).
        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);

        // Processor + health tracker — Scenarios 2, 3, 5 invoke RunOnceAsync directly.
        services.AddSingleton<IAuditOutboxHealthTracker, AuditOutboxHealthTracker>();
        services.AddSingleton<AuditOutboxProcessor>();

        // Scenario 4 injects a throwing best-effort AuditService.
        if (auditServiceOverride is not null)
        {
            services.Replace(ServiceDescriptor.Scoped<AuditService>(_ => auditServiceOverride));
        }

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(db);

        return new TestContextEnv
        {
            Sp = sp,
            Db = db,
            DatabaseName = dbName,
            Fixture = _fixture,
        };
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db, string email, string role)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id          = id,
            Email       = email,
            DisplayName = email.Split('@')[0],
            Role        = role,
            Tier        = "free",
            CreatedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();
        return id;
    }

    private static AuditOutboxPayload BuildPayload(string action, DateTimeOffset timestamp)
        => new()
        {
            Action      = action,
            Resource    = "TestResource",
            UserId      = Guid.NewGuid().ToString(),
            ResourceId  = "test-resource-id",
            Result      = "Success",
            IpAddress   = "127.0.0.1",
            UserAgent   = "xunit",
            RequestType = "TestCommand",
            Details     = "{}",
            Timestamp   = timestamp,
            Oversize    = false,
        };

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

    /// <summary>
    /// Test double whose <see cref="AuditService.EnqueueAuditAsync"/> always throws — used by
    /// Scenario 4 to prove the best-effort path swallows audit failures and keeps the business
    /// mutation. Constructor satisfies the base ctor with an unused InMemory DbContext (the
    /// override never touches it).
    /// </summary>
    private sealed class ThrowingBestEffortAuditService : AuditService
    {
        public ThrowingBestEffortAuditService()
            : base(
                new MeepleAiDbContext(
                    new DbContextOptionsBuilder<MeepleAiDbContext>()
                        .UseInMemoryDatabase($"throwing_besteffort_audit_{Guid.NewGuid()}")
                        .Options,
                    Mock.Of<IMediator>(),
                    Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>()),
                Mock.Of<ILogger<AuditService>>(),
                timeProvider: null)
        {
        }

        public override Task EnqueueAuditAsync(
            AuditOutboxPayload payload,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Scenario 4: simulated best-effort audit enqueue failure (audit_outbox unreachable).");
        }
    }
}

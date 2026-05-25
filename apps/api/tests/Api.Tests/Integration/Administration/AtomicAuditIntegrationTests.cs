using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests proving the [AtomicAudit] transaction guarantee for DeleteUserCommand.
///
/// Key assertions:
///   1. Happy path: user is hard-deleted AND an audit_outbox row is written in the same commit.
///   2. Failure path: when EnqueueAuditAtomicAsync throws, the whole transaction rolls back —
///      the user still exists AND no audit_outbox row is present.
///
/// The forced-failure test injects a stub AuditService that overrides EnqueueAuditAtomicAsync
/// to throw. Since the behavior calls it inside the open transaction before CommitAsync, the
/// throw causes `await using tx` to dispose without commit → full rollback.
///
/// SP5 Admin Security S1 — Task 3b atomicity guarantee.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class AtomicAuditIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AtomicAuditIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_atomic_audit_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = BuildServices(connectionString, auditServiceOverride: null);
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

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Happy-path: mutation + audit row committed atomically
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_Atomic_CommitsBoth_MutationAndOutbox()
    {
        // Arrange: seed requesting admin (superadmin) + second admin (target) + another admin so
        // the "last admin" guard passes (we need at least 2 admins when deleting an admin).
        using var scope = _serviceProvider!.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MeepleAiDbContext>();
        var mediator = sp.GetRequiredService<IMediator>();

        var requestingAdminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var anotherAdminId = Guid.NewGuid();

        // Seed a regular (non-admin) target user — simplest case (no admin-count guard)
        db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = requestingAdminId,
            Email = $"requesting-{requestingAdminId:N}@test.local",
            DisplayName = "Requesting Admin",
            Role = "superadmin",
            Tier = "enterprise",
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = targetUserId,
            Email = $"target-{targetUserId:N}@test.local",
            DisplayName = "Target User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = anotherAdminId,
            Email = $"another-{anotherAdminId:N}@test.local",
            DisplayName = "Another Admin",
            Role = "admin",
            Tier = "enterprise",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();

        // Act: dispatch DeleteUserCommand through the full MediatR pipeline
        var command = new DeleteUserCommand(
            UserId: targetUserId.ToString(),
            RequestingUserId: requestingAdminId.ToString());

        var act = () => mediator.Send(command, TestCancellationToken);
        await act.Should().NotThrowAsync(
            because: "DeleteUserCommand with valid input should succeed");

        // Assert 1: user is hard-deleted (Users.Remove was called, committed inside the tx)
        db.ChangeTracker.Clear();
        var userStillExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == targetUserId, TestCancellationToken);
        userStillExists.Should().BeFalse(
            because: "the target user must be hard-deleted after DeleteUserCommand commits");

        // Assert 2: exactly one Pending audit_outbox row with action "UserDelete"
        var outboxRows = await db.AuditOutbox
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);
        outboxRows.Should().HaveCount(1,
            because: "[AtomicAudit] DeleteUserCommand must write exactly one outbox row");

        var row = outboxRows[0];
        row.Status.Should().Be(Api.Infrastructure.Entities.OutboxStatus.Pending,
            because: "the outbox row must be Pending — the T4 processor has not run");

        var payload = JsonSerializer.Deserialize<AuditOutboxPayload>(row.PayloadJson);
        payload.Should().NotBeNull();
        payload!.Action.Should().Be("UserDelete");
        payload.Resource.Should().Be("User");
        payload.Result.Should().Be("Success");
        payload.RequestType.Should().Be(nameof(DeleteUserCommand));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Failure path: audit write failure rolls back the mutation too
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_Atomic_WhenAuditWriteFails_RollsBackMutation()
    {
        // This test proves the core atomicity guarantee:
        //   IF EnqueueAuditAtomicAsync throws INSIDE the open transaction,
        //   THEN the whole transaction rolls back — the user still exists AND no outbox row.
        //
        // Mechanism: we build a fresh ServiceProvider that injects a ThrowingAuditService whose
        // EnqueueAuditAtomicAsync unconditionally throws. Because the behavior calls it inside
        // the `await using var tx` block, the throw causes tx.DisposeAsync() without CommitAsync
        // → PostgreSQL rolls back both the Users.Remove + the audit_outbox INSERT.

        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            $"test_atomic_rollback_{Guid.NewGuid():N}");

        var throwingAudit = new ThrowingAuditService();
        await using var sp = BuildServices(connectionString, auditServiceOverride: throwingAudit)
            .BuildServiceProvider();

        var db = sp.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(db);

        // Seed target user
        var requestingAdminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = requestingAdminId,
            Email = $"req-{requestingAdminId:N}@test.local",
            DisplayName = "Requesting Admin",
            Role = "superadmin",
            Tier = "enterprise",
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = targetUserId,
            Email = $"tgt-{targetUserId:N}@test.local",
            DisplayName = "Target User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();

        // Act: dispatch; should throw because EnqueueAuditAtomicAsync throws
        var mediator = sp.GetRequiredService<IMediator>();
        var command = new DeleteUserCommand(
            UserId: targetUserId.ToString(),
            RequestingUserId: requestingAdminId.ToString());

        var act = () => mediator.Send(command, TestCancellationToken);
        await act.Should().ThrowAsync<Exception>(
            because: "audit failure in atomic path must propagate to the caller");

        // Assert 1: user still exists — the mutation was rolled back
        db.ChangeTracker.Clear();
        var userStillExists = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(u => u.Id == targetUserId, TestCancellationToken);
        userStillExists.Should().BeTrue(
            because: "the transaction rolled back — user must NOT be deleted");

        // Assert 2: no outbox rows — the audit INSERT was rolled back too
        var outboxCount = await db.AuditOutbox
            .AsNoTracking()
            .CountAsync(TestCancellationToken);
        outboxCount.Should().Be(0,
            because: "the transaction rolled back — no audit_outbox row must exist");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    private static ServiceCollection BuildServices(string connectionString, AuditService? auditServiceOverride)
    {
        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Register IUserRepository — required by DeleteUserCommandHandler
        services.AddScoped<IUserRepository, UserRepository>();

        // Register IHttpContextAccessor — behavior needs it; null HttpContext is handled gracefully
        services.AddSingleton<IHttpContextAccessor>(
            new Mock<IHttpContextAccessor>().Object);

        // Register the AuditLoggingBehavior pipeline behavior (not wired by CreateBase)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));

        // Override AuditService if a test double was provided (for the failure-rollback test).
        // Use Replace() so the existing Scoped registration from CreateBase is removed first —
        // .NET DI with multiple AddScoped<T> calls returns the LAST registration, but it's
        // cleaner and more explicit to Replace() to ensure exactly one registration exists.
        if (auditServiceOverride is not null)
        {
            services.Replace(ServiceDescriptor.Scoped<AuditService>(_ => auditServiceOverride));
        }

        return services;
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

    /// <summary>
    /// Test double for AuditService whose EnqueueAuditAtomicAsync always throws.
    /// Used to prove that a mid-transaction audit failure rolls back the mutation.
    /// The service still needs a valid MeepleAiDbContext for its base constructor — we
    /// use an InMemory DB for constructor satisfaction since its methods are not invoked.
    /// </summary>
    private sealed class ThrowingAuditService : AuditService
    {
        public ThrowingAuditService()
            : base(
                new MeepleAiDbContext(
                    new DbContextOptionsBuilder<MeepleAiDbContext>()
                        .UseInMemoryDatabase($"throwing_audit_{Guid.NewGuid()}")
                        .Options,
                    Mock.Of<IMediator>(),
                    Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>()),
                Mock.Of<Microsoft.Extensions.Logging.ILogger<AuditService>>(),
                timeProvider: null)
        {
        }

        public override Task EnqueueAuditAtomicAsync(
            AuditOutboxPayload payload,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated audit write failure — verifies transaction rollback in atomic path.");
        }
    }
}

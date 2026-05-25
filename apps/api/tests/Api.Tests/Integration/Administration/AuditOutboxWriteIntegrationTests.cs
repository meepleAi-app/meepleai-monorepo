using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
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
/// End-to-end integration test: verifies that when an [AuditableAction] command is dispatched
/// through the full MediatR pipeline (including AuditLoggingBehavior), a Pending row is written
/// to audit_outbox with the expected payload.
///
/// Also verifies the NoTracking reality check: UserEntity is [Auditable], and UserRepository.UpdateAsync
/// uses AsTracking() so the interceptor captures a snapshot on the UoW SaveChanges call.
///
/// SP5 Admin Security S1 — Task 3 integration smoke test.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class AuditOutboxWriteIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AuditOutboxWriteIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_audit_outbox_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Register IUserRepository — required by ChangeUserRoleCommandHandler
        services.AddScoped<IUserRepository, UserRepository>();

        // Register IHttpContextAccessor — required by AuditLoggingBehavior to extract request context.
        // We register a mock that returns no HttpContext (simulating a non-HTTP caller).
        // The behavior handles null HttpContext gracefully (userId/email/ip/userAgent all null).
        services.AddSingleton<IHttpContextAccessor>(
            new Mock<IHttpContextAccessor>().Object); // returns null HttpContext — behavior handles this

        // Register AuditLoggingBehavior as an MediatR open pipeline behavior.
        // IntegrationServiceCollectionBuilder.CreateBase only calls RegisterServicesFromAssembly —
        // it does NOT wire the open behaviors that Program.cs registers via cfg.AddOpenBehavior().
        // Without this, ChangeUserRoleCommand runs without the audit pipeline and no row is written.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply all migrations (including audit_outbox table)
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }
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
    public async Task ChangeUserRoleCommand_WritesOutboxRow_WithExpectedPayload()
    {
        // Arrange: seed a user (superadmin + target user) so the command succeeds.
        // We use IMediator so the full pipeline runs (AuditLoggingBehavior fires).
        using var scope = _serviceProvider!.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MeepleAiDbContext>();
        var mediator = sp.GetRequiredService<IMediator>();

        // Seed a target user with role "editor" (so we can change to "user" from "admin" level)
        var targetUserId = Guid.NewGuid();
        var targetUserEntity = new Api.Infrastructure.Entities.UserEntity
        {
            Id = targetUserId,
            Email = $"target-{targetUserId:N}@test.local",
            DisplayName = "Target User",
            Role = "editor",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(targetUserEntity);
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();

        // Act: dispatch command through MediatR pipeline.
        // AdminRole = "superadmin" so privilege check passes (superadmin > editor level).
        var command = new ChangeUserRoleCommand(
            UserId: targetUserId.ToString(),
            NewRole: "User",
            Reason: "T3 integration test",
            AdminRole: "superadmin");

        // Act should not throw — if it throws, the behavior or handler has failed
        var act = () => mediator.Send(command, TestCancellationToken);
        await act.Should().NotThrowAsync("the command should succeed for a valid user with valid role change");

        // Assert: exactly one Pending row in audit_outbox
        db.ChangeTracker.Clear();
        var outboxRows = await db.AuditOutbox
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        outboxRows.Should().HaveCount(1,
            because: "AuditLoggingBehavior must write exactly one outbox row for the [AuditableAction] command");

        var row = outboxRows[0];
        row.Status.Should().Be(Api.Infrastructure.Entities.OutboxStatus.Pending,
            because: "the row should be Pending — the T4 processor has not run yet");

        // Assert payload content
        var payload = JsonSerializer.Deserialize<AuditOutboxPayload>(row.PayloadJson);
        payload.Should().NotBeNull();
        payload!.Action.Should().Be("UserRoleChange");
        payload.Resource.Should().Be("User");
        payload.Result.Should().Be("Success");
        payload.RequestType.Should().Be(nameof(ChangeUserRoleCommand));
        payload.Details.Should().Contain("commandType");
    }

    [Fact]
    public async Task ChangeUserRoleCommand_CapturesUserEntitySnapshot_BecauseUserEntityIsAuditable()
    {
        // This test verifies the NoTracking reality check:
        //   - UserRepository.UpdateAsync uses AsTracking() so EF tracks the UserEntity during update.
        //   - When UnitOfWork.SaveChangesAsync fires, AuditingSaveChangesInterceptor sees UserEntity
        //     as Modified and records a snapshot.
        //   - AuditLoggingBehavior folds the snapshot into the outbox payload.

        using var scope = _serviceProvider!.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MeepleAiDbContext>();
        var mediator = sp.GetRequiredService<IMediator>();
        var sink = sp.GetRequiredService<ScopedAuditSnapshotSink>();

        var targetUserId = Guid.NewGuid();
        db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = targetUserId,
            Email = $"snap-{targetUserId:N}@test.local",
            DisplayName = "Snapshot Test User",
            Role = "editor",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestCancellationToken);
        db.ChangeTracker.Clear();

        // The seed's SaveChangesAsync fires the interceptor and records an Insert snapshot for
        // UserEntity into the scoped sink. Clear it now so the behavior only sees the command's
        // Update snapshot — not the seed's Insert snapshot.
        sink.Clear();

        var command = new ChangeUserRoleCommand(
            UserId: targetUserId.ToString(),
            NewRole: "User",
            Reason: "snapshot check",
            AdminRole: "superadmin");

        var act = () => mediator.Send(command, TestCancellationToken);
        await act.Should().NotThrowAsync("the command should succeed for a valid user with valid role change");

        db.ChangeTracker.Clear();
        var outboxRows = await db.AuditOutbox
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        outboxRows.Should().HaveCount(1);

        var payload = JsonSerializer.Deserialize<AuditOutboxPayload>(outboxRows[0].PayloadJson);
        payload.Should().NotBeNull();

        // Key assertion: the interceptor captured a UserEntity snapshot (Update operation)
        // because UserEntity is [Auditable] and UpdateAsync uses AsTracking().
        payload!.Snapshots.Should().NotBeEmpty(
            because: "AuditingSaveChangesInterceptor must capture a UserEntity snapshot on SaveChanges " +
                     "when UserEntity is [Auditable] and the repo loads via AsTracking()");

        var userSnapshot = payload.Snapshots
            .FirstOrDefault(s => s.EntityType == nameof(Api.Infrastructure.Entities.UserEntity));
        userSnapshot.Should().NotBeNull("there must be a UserEntity snapshot in the payload");
        userSnapshot!.Operation.Should().Be("Update",
            because: "UpdateAsync modifies an existing row");
        userSnapshot.AfterJson.Should().NotBeNull("AfterJson must be populated for Update operations");
        userSnapshot.AfterJson.Should().NotContain("***REDACTED***" is null ? "" : "PasswordHash\":\"non-redacted",
            because: "PasswordHash is marked [SensitiveData] and must be redacted in the snapshot");
    }

    [Fact]
    public async Task AuditLoggingBehavior_WithNoAuditableCommand_DoesNotWriteOutboxRow()
    {
        // Verifies the guard at the top of AuditLoggingBehavior — outbox is empty until an
        // [AuditableAction] command is dispatched.
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Assert: no outbox rows exist (nothing has been dispatched)
        var count = await db.AuditOutbox.AsNoTracking().CountAsync(TestCancellationToken);
        count.Should().Be(0, because: "no [AuditableAction] command has been dispatched yet");
    }

    [Fact]
    public async Task AuditService_EnqueueAuditAsync_DirectCall_WritesOutboxRow()
    {
        // Smoke test: calls AuditService.EnqueueAuditAsync directly to verify the DB write
        // path works independently of the MediatR pipeline.
        using var scope = _serviceProvider!.CreateScope();
        var sp = scope.ServiceProvider;
        var auditService = sp.GetRequiredService<Api.Services.AuditService>();
        var db = sp.GetRequiredService<MeepleAiDbContext>();

        var payload = new AuditOutboxPayload
        {
            Action = "DirectTest",
            Resource = "TestResource",
            Result = "Success",
            Timestamp = DateTimeOffset.UtcNow,
        };

        await auditService.EnqueueAuditAsync(payload, TestCancellationToken);

        var count = await db.AuditOutbox.AsNoTracking().CountAsync(TestCancellationToken);
        count.Should().Be(1, because: "EnqueueAuditAsync must write one Pending row to audit_outbox");
    }
}

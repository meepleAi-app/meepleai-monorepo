using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests isolating the access-request APPROVAL flow (happy-path testing 2026-07-10, #B).
/// Repro target: after approving, access_requests.status must become "Approved" and invitation_id
/// must be populated — observed in the running app to stay Pending/null while a token IS created.
/// This test isolates the COMMAND (no outbox processor running) to determine whether the command
/// itself persists the Approved status.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
public sealed class AccessRequestApprovalIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider _serviceProvider = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid AdminUserId = new("a0000000-0000-0000-0000-000000000001");

    public AccessRequestApprovalIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_access_approval_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var enforced = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        var services = IntegrationServiceCollectionBuilder.CreateBase(enforced.ConnectionString);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(DateTimeOffset.UtcNow));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IInvitationTokenRepository, InvitationTokenRepository>();
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await TestMigrationHelper.MigrateWithRetryAsync(_dbContext, Ct);

        // Seed admin (approver)
        var userRepo = _serviceProvider.GetRequiredService<IUserRepository>();
        var uow = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var admin = new Api.BoundedContexts.Authentication.Domain.Entities.User(
            AdminUserId,
            new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email("admin@test.meepleai.dev"),
            "Test Admin",
            Api.BoundedContexts.Authentication.Domain.ValueObjects.PasswordHash.Create("AdminUnusualPwd123!"),
            Api.SharedKernel.Domain.ValueObjects.Role.Admin);
        await userRepo.AddAsync(admin, Ct);
        await uow.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable d) await d.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Approve_CommandAlone_PersistsApprovedStatus()
    {
        // Arrange — a pending access request committed to the DB.
        var repo = _serviceProvider.GetRequiredService<IAccessRequestRepository>();
        var uow = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        var request = AccessRequest.Create("approve-iso@test.dev");
        var requestId = request.Id;
        await repo.AddAsync(request, Ct);
        await uow.SaveChangesAsync(Ct);

        // Act — approve via the command. OutboxOnly + no processor registered here, so the
        // AccessRequestApprovedEvent handler does NOT fire. This isolates the command's own persistence.
        await mediator.Send(new ApproveAccessRequestCommand(requestId, AdminUserId), Ct);

        // Assert — read the committed row directly (fresh, no tracking).
        _dbContext.ChangeTracker.Clear();
        var committed = await _dbContext.AccessRequests.AsNoTracking()
            .FirstAsync(e => e.Id == requestId, Ct);
        committed.Status.Should().Be("Approved",
            "the approve command must persist the Approved status independently of the event handler");
        committed.ReviewedBy.Should().Be(AdminUserId);
    }

    [Fact]
    public async Task Approve_ThenDispatchEventInSeparateScope_KeepsApprovedAndCorrelatesInvitation()
    {
        // Arrange
        var repo = _serviceProvider.GetRequiredService<IAccessRequestRepository>();
        var uow = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        var request = AccessRequest.Create("approve-e2e@test.dev");
        var requestId = request.Id;
        var email = request.Email;
        await repo.AddAsync(request, Ct);
        await uow.SaveChangesAsync(Ct);

        // 1. Approve via command — commits Approved status + enqueues the outbox event.
        await mediator.Send(new ApproveAccessRequestCommand(requestId, AdminUserId), Ct);

        // 2. Reproduce the DomainEventOutboxProcessor: it dispatches the event in a SEPARATE
        //    scope (its own DbContext) and calls SaveChanges after the handler runs (line 230).
        using (var procScope = _serviceProvider.CreateScope())
        {
            var procMediator = procScope.ServiceProvider.GetRequiredService<IMediator>();
            var procDb = procScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await procMediator.Publish(new AccessRequestApprovedEvent(requestId, email, AdminUserId), Ct);
            await procDb.SaveChangesAsync(Ct);
        }

        // 3. Assert final persisted state (fresh read).
        _dbContext.ChangeTracker.Clear();
        var final = await _dbContext.AccessRequests.AsNoTracking().FirstAsync(e => e.Id == requestId, Ct);
        final.Status.Should().Be("Approved",
            "status must remain Approved after the async event handler runs");
        final.InvitationId.Should().NotBeNull(
            "the event handler must persist the invitation correlation id (idempotency guard #1940)");
    }

    [Fact]
    public async Task MarkNotified_PartialUpdate_DoesNotClobberApprovedStatus()
    {
        // Arrange — a pending request, then approved.
        var repo = _serviceProvider.GetRequiredService<IAccessRequestRepository>();
        var uow = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        var request = AccessRequest.Create("guard@test.dev");
        var id = request.Id;
        await repo.AddAsync(request, Ct);
        await uow.SaveChangesAsync(Ct);
        await mediator.Send(new ApproveAccessRequestCommand(id, AdminUserId), Ct);

        // Act — the Created-event notification guard persists AFTER the approval. The partial
        // update (#B fix) writes only last_notified_event_id via direct SQL.
        var eventId = Guid.NewGuid();
        await repo.MarkNotifiedAsync(id, eventId, Ct);

        // Assert — approval survives, guard persisted.
        _dbContext.ChangeTracker.Clear();
        var final = await _dbContext.AccessRequests.AsNoTracking().FirstAsync(e => e.Id == id, Ct);
        final.Status.Should().Be("Approved",
            "the notification-guard partial update must not clobber a concurrent approval (#B fix)");
        final.LastNotifiedEventId.Should().Be(eventId);
    }

    [Fact]
    public async Task SetInvitationId_PartialUpdate_DoesNotClobberApprovedStatus()
    {
        // Arrange
        var repo = _serviceProvider.GetRequiredService<IAccessRequestRepository>();
        var invRepo = _serviceProvider.GetRequiredService<IInvitationTokenRepository>();
        var uow = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        var request = AccessRequest.Create("corr@test.dev");
        var id = request.Id;
        await repo.AddAsync(request, Ct);
        await uow.SaveChangesAsync(Ct);
        await mediator.Send(new ApproveAccessRequestCommand(id, AdminUserId), Ct);

        // A real invitation token must exist — FK access_requests.invitation_id → invitation_tokens.id.
        var token = InvitationToken.Create("corr@test.dev", "User", "test-token-hash", AdminUserId);
        await invRepo.AddAsync(token, Ct);
        await uow.SaveChangesAsync(Ct);
        var invitationId = token.Id;

        // Act — the Approved-event handler correlates the invitation via the partial update.
        await repo.SetInvitationIdAsync(id, invitationId, Ct);

        // Assert — approval survives, correlation persisted (idempotency guard #1940 now works).
        _dbContext.ChangeTracker.Clear();
        var final = await _dbContext.AccessRequests.AsNoTracking().FirstAsync(e => e.Id == id, Ct);
        final.Status.Should().Be("Approved");
        final.InvitationId.Should().Be(invitationId);
    }
}

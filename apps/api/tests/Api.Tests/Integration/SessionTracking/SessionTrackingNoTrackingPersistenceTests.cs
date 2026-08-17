using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Regression for #2734: the sibling NoTracking silent no-op cluster surfaced while
/// investigating #2660. The DbContext defaults to <c>QueryTrackingBehavior.NoTracking</c>
/// (PERF-06), so a repository getter that loads an entity, hands it to a handler that mutates
/// it via a domain method, and then calls <c>SaveChangesAsync</c> persists NOTHING unless the
/// getter opts back into tracking with <c>.AsTracking()</c> — the mutation is a silent no-op.
///
/// Two getters were affected (verified):
///  - <see cref="IGamebookCampaignSessionRepository.GetByIdAsync"/> → Rename() / Touch()
///  - <see cref="IToolkitSessionStateRepository.GetBySessionAsync"/> → UpdateWidgetState() (update path)
///
/// Existing Category=Unit tests missed this because they mock the repository and never exercise
/// the real EF NoTracking pipeline.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "2734")]
public sealed class SessionTrackingNoTrackingPersistenceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"session_tracking_notracking_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public SessionTrackingNoTrackingPersistenceTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(conn);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "#2734: GamebookCampaignSession.Rename persists the Title scalar through the getter")]
    public async Task RenameGamebookCampaign_PersistsTitle()
    {
        Guid campaignId;
        Guid ownerId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookCampaignSessionRepository>();

            (ownerId, _) = await TestSessionHelper.CreateUserSessionAsync(db);
            var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Original Title");
            await repo.AddAsync(session, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
            campaignId = session.Id;
        }

        // Replicates RenameGamebookCampaignHandler: load via getter, mutate via domain method, save.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookCampaignSessionRepository>();

            var session = await repo.GetByIdAsync(campaignId, CancellationToken.None);
            session!.Rename("Renamed Title", ownerId);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.GamebookCampaignSessions.AsNoTracking().FirstAsync(x => x.Id == campaignId);
            entity.Title.Should().Be("Renamed Title",
                "GetByIdAsync must load .AsTracking() so Rename() is persisted (#2734 — NoTracking silent no-op)");
        }
    }

    [Fact(DisplayName = "#2734: GamebookCampaignSession.Touch persists the UpdatedBy scalar through the getter")]
    public async Task TouchGamebookCampaign_PersistsUpdatedBy()
    {
        Guid campaignId;
        Guid ownerId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookCampaignSessionRepository>();

            (ownerId, _) = await TestSessionHelper.CreateUserSessionAsync(db);
            var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Progress Campaign");
            await repo.AddAsync(session, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
            campaignId = session.Id;
        }

        // Replicates UpdateGamebookProgressHandler's parent-session Touch(): load, mutate, save.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookCampaignSessionRepository>();

            var session = await repo.GetByIdAsync(campaignId, CancellationToken.None);
            session!.Touch(ownerId);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.GamebookCampaignSessions.AsNoTracking().FirstAsync(x => x.Id == campaignId);
            entity.UpdatedBy.Should().Be(ownerId,
                "GetByIdAsync must load .AsTracking() so Touch() is persisted (#2734 — NoTracking silent no-op)");
        }
    }

    [Fact(DisplayName = "#2734: GamebookCampaignSession.SoftDelete persists IsDeleted through the getter (data-integrity: HasQueryFilter)")]
    public async Task SoftDeleteGamebookCampaign_PersistsIsDeleted()
    {
        Guid campaignId;
        Guid ownerId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookCampaignSessionRepository>();

            (ownerId, _) = await TestSessionHelper.CreateUserSessionAsync(db);
            var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Doomed Campaign");
            await repo.AddAsync(session, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
            campaignId = session.Id;
        }

        // Replicates DeleteGamebookCampaignHandler: load via getter, SoftDelete(), save.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookCampaignSessionRepository>();

            var session = await repo.GetByIdAsync(campaignId, CancellationToken.None);
            session!.SoftDelete(ownerId);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            // IgnoreQueryFilters(): the global HasQueryFilter(e => !e.IsDeleted) would otherwise
            // hide the row. Assert the column actually flipped — a NoTracking no-op would leave
            // is_deleted=false, leaving the "deleted" campaign visible forever (data-integrity bug).
            var entity = await db.GamebookCampaignSessions.IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(x => x.Id == campaignId);
            entity.IsDeleted.Should().BeTrue(
                "GetByIdAsync must load .AsTracking() so SoftDelete() flips is_deleted (#2734 — HasQueryFilter makes this a data-integrity bug)");
            entity.DeletedAt.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "#2734: ToolkitSessionState.UpdateWidgetState persists the widget state (update path) through the getter")]
    public async Task UpdateWidgetState_PersistsWidgetStates()
    {
        var sessionId = Guid.NewGuid();
        var toolkitId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IToolkitSessionStateRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var state = ToolkitSessionState.Create(sessionId, toolkitId);
            await repo.AddAsync(state, CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Replicates UpdateWidgetStateCommandHandler's update path (state already exists):
        // load via getter, mutate via domain method, save via unit of work.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IToolkitSessionStateRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var state = await repo.GetBySessionAsync(sessionId, toolkitId, CancellationToken.None);
            state!.UpdateWidgetState("TurnManager", "{\"turn\":3}");
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.ToolkitSessionStates.AsNoTracking().FirstAsync(x => x.SessionId == sessionId && x.ToolkitId == toolkitId);
            entity.WidgetStates.Should().ContainKey("TurnManager",
                "GetBySessionAsync must load .AsTracking() so UpdateWidgetState() is persisted on the update path (#2734 — NoTracking silent no-op)");
            entity.WidgetStates["TurnManager"].Should().Be("{\"turn\":3}");
        }
    }
}

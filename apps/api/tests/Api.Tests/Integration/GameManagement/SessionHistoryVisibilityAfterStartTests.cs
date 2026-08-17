using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for Issue #2587 Slice 3 — end-to-end proof that the correlated
/// GameSession is visible through GetActiveSessionsQuery (the exact query the FE uses
/// via api.sessions.getActive) after the wizard flow.
///
/// BEFORE the #2587 fix (Slices 1+2): wizard-created LiveGameSessions never created a
/// correlated GameSession, so GetActiveSessionsQuery returned an empty Sessions list.
/// AFTER the fix: StartLiveSessionCommandHandler creates the correlated GameSession
/// atomically; it immediately appears in GetActiveSessionsQuery.Sessions.
///
/// T1 — History-visible on start: full wizard flow → GetActiveSessionsQuery.Sessions
///      contains a session with Id == CorrelatedGameSessionId (correct GameId + creator).
/// T2 — Drops out on complete: CompleteLiveSessionCommand → correlated GameSession no
///      longer appears in GetActiveSessionsQuery + appears in GetSessionHistoryQuery.
/// T3 — Free-form not in active list: free-form (GameId==null) start → no correlated
///      GameSession → GetActiveSessionsQuery.Sessions stays empty.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionHistoryVisibilityAfterStartTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"history_visibility_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public SessionHistoryVisibilityAfterStartTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Database.Migrate();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
            await _factory.DisposeAsync();

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T1 — After the wizard flow (create→addPlayer→start) the correlated
    //      GameSession appears in GetActiveSessionsQuery.Sessions.
    //
    // This is the key regression test for #2587: before the fix, the Sessions
    // list was always empty for wizard-created sessions because no GameSession
    // row existed to be found by IGameSessionRepository.FindActiveAsync.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T1: GetActiveSessionsQuery contains correlated GameSession after wizard start")]
    public async Task T1_AfterWizardStart_GetActiveSessionsQuery_ContainsCorrelatedSession()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        // Confirm baseline: no active sessions before the wizard flow
        var before = await mediator.Send(new GetActiveSessionsQuery());
        before.Sessions.Should().BeEmpty("no sessions exist before the wizard flow");

        // Full wizard flow (mirrors what the FE wizard does)
        var liveSessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Mage Knight",
            GameId: gameId));

        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: liveSessionId,
            DisplayName: "Aaron",
            Color: PlayerColor.Red,
            UserId: userId));

        // Act — start the live session (Slice 1+2: this now creates the correlated GameSession)
        await mediator.Send(new StartLiveSessionCommand(liveSessionId, userId, UserTier.Free, Role.User));

        // Fetch CorrelatedGameSessionId from the live session (persisted by StartLiveSessionCommandHandler)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var live = await verifyDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveSessionId);
        live.CorrelatedGameSessionId.Should().NotBeNull(
            "StartLiveSessionCommandHandler (Slice 1) must set CorrelatedGameSessionId");

        var correlatedId = live.CorrelatedGameSessionId!.Value;

        // Assert — GetActiveSessionsQuery (= api.sessions.getActive FE call) returns the correlated session
        var verifyMediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await verifyMediator.Send(new GetActiveSessionsQuery());

        result.Sessions.Should().NotBeEmpty(
            "GetActiveSessionsQuery must return at least the newly correlated GameSession " +
            "— before #2587 fix this list was always empty for wizard-created sessions");

        result.Sessions.Should().ContainSingle(
            s => s.Id == correlatedId,
            because: "the correlated GameSession (Id == CorrelatedGameSessionId) must appear " +
                     "in the active-sessions list immediately after the wizard start flow");

        var dto = result.Sessions.Single(s => s.Id == correlatedId);
        dto.GameId.Should().Be(gameId,
            "the correlated GameSession must be linked to the same catalog game as the LiveGameSession");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T2 — After CompleteLiveSession the correlated GameSession drops out of
    //      GetActiveSessionsQuery AND appears in GetSessionHistoryQuery.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2: Completed correlated session drops from active list and appears in history")]
    public async Task T2_AfterComplete_DropsFromActiveQuery_AppearsInHistoryQuery()
    {
        // Arrange — run the full wizard flow
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        var liveSessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Wingspan",
            GameId: gameId));

        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: liveSessionId,
            DisplayName: "Aaron",
            Color: PlayerColor.Green,
            UserId: userId));

        await mediator.Send(new StartLiveSessionCommand(liveSessionId, userId, UserTier.Free, Role.User));

        // Confirm the correlated session is active
        await using var preScope = _factory.Services.CreateAsyncScope();
        var preMediator = preScope.ServiceProvider.GetRequiredService<IMediator>();
        var activeBeforeComplete = await preMediator.Send(new GetActiveSessionsQuery());
        activeBeforeComplete.Sessions.Should().NotBeEmpty(
            "pre-condition: session must be active before completion");

        var preDb = preScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var livePre = await preDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveSessionId);
        var correlatedId = livePre.CorrelatedGameSessionId!.Value;

        activeBeforeComplete.Sessions.Should().Contain(s => s.Id == correlatedId,
            "pre-condition: correlated session must be visible as active before complete");

        // Act — complete the live session (Slice 2: this marks the correlated GameSession as Completed)
        await using var completeScope = _factory.Services.CreateAsyncScope();
        var completeMediator = completeScope.ServiceProvider.GetRequiredService<IMediator>();
        await completeMediator.Send(new CompleteLiveSessionCommand(liveSessionId));

        // Assert — correlated session is GONE from the active-sessions query
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyMediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();

        var activeAfterComplete = await verifyMediator.Send(new GetActiveSessionsQuery());
        activeAfterComplete.Sessions.Should().NotContain(
            s => s.Id == correlatedId,
            because: "a Completed GameSession must not be returned by FindActiveAsync " +
                     "(which filters on Setup/InProgress/Paused only); " +
                     "completing the LiveGameSession must also complete the correlated GameSession");

        // Assert — correlated session appears in session history (Completed / Abandoned filter)
        var history = await verifyMediator.Send(new GetSessionHistoryQuery(GameId: gameId));
        history.Should().Contain(
            s => s.Id == correlatedId,
            because: "GetSessionHistoryQuery (FindHistoryAsync) must now find the " +
                     "correlated GameSession with Status == Completed");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3 — Free-form (GameId == null) start does NOT add anything to the
    //      active-sessions list (no correlated GameSession created).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3: Free-form start (no GameId) does not appear in GetActiveSessionsQuery")]
    public async Task T3_FreeFormStart_DoesNotAddToActiveSessionsQuery()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);

        // Free-form: GameId explicitly null
        var liveSessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Quick Improvised Game",
            GameId: null));

        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: liveSessionId,
            DisplayName: "Aaron",
            Color: PlayerColor.Blue,
            UserId: userId));

        // Act — start succeeds but no correlated GameSession should be created
        await mediator.Send(new StartLiveSessionCommand(liveSessionId, userId, UserTier.Free, Role.User));

        // Assert — CorrelatedGameSessionId is null (no GameSession created)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var live = await verifyDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveSessionId);
        live.CorrelatedGameSessionId.Should().BeNull(
            "free-form sessions (GameId == null) must not create a correlated GameSession");

        // Assert — GetActiveSessionsQuery returns nothing for this user's session
        var verifyMediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await verifyMediator.Send(new GetActiveSessionsQuery());

        result.Sessions.Should().BeEmpty(
            "a free-form LiveGameSession creates no correlated GameSession; " +
            "GetActiveSessionsQuery (FindActiveAsync on GameSession table) must remain empty");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"hist-vis-{userId:N}@test.local",
            DisplayName = "History Visibility Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return userId;
    }

    private static async Task<Guid> SeedSharedGameAsync(MeepleAiDbContext db)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"History Visibility Test Game {gameId:N}",
            YearPublished = 2024,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return gameId;
    }
}

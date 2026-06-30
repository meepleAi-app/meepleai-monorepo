using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.Exceptions;
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
/// Integration tests for Issue #2587 Slice 1 — correlated GameSession at start + quota + lifecycle sync.
///
/// Proves the four acceptance criteria:
/// T1  — GameId-backed start → GameSession created + CorrelatedGameSessionId persisted + active count +1.
/// T2  — Quota enforced: starting beyond the Free limit (3) throws QuotaExceededException.
/// T3  — Complete → correlated GameSession is Completed + active count drops back to 0.
/// T4  — Free-form (GameId == null) start → no GameSession created; start succeeds.
///
/// Tier limits (SessionQuotaService defaults):
///   Free  = 3 active sessions max (controllable via config key "SessionLimits:free:MaxSessions")
///   Normal = 10 max
///   Premium = unlimited
///
/// These tests use IntegrationWebApplicationFactory (which mocks Redis/embeddings) and
/// SharedTestcontainersFixture (shared Postgres container, isolated DB per test class).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CorrelatedGameSessionOnStartTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"correlated_quota_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public CorrelatedGameSessionOnStartTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

        // Override the Free-tier session limit to 1 so T2 (quota enforcement) is cheap:
        // start 1 session → next start fails without needing 3 round-trips.
        _factory = IntegrationWebApplicationFactory.Create(
            connectionString,
            extraConfig: new Dictionary<string, string?>
            {
                ["SessionLimits:free:MaxSessions"] = "1"
            });

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
    // T1 — Start GameId-backed session: GameSession row created + correlated +
    //      active-sessions count becomes 1 (history-visibility + quota slot).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T1: Start GameId-backed live session creates correlated GameSession and increments active count")]
    public async Task T1_Start_GameIdBacked_CreatesCorrelatedGameSession_And_CountsActive()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameSessionRepo = scope.ServiceProvider.GetRequiredService<IGameSessionRepository>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        // Active count is 0 before start
        var countBefore = await gameSessionRepo.CountActiveByUserIdAsync(userId);
        countBefore.Should().Be(0, "no sessions exist yet");

        var liveSessionId = await mediator.Send(new CreateLiveSessionCommand(UserId: userId, GameName: "Mage Knight", GameId: gameId));
        await mediator.Send(new AddPlayerToLiveSessionCommand(SessionId: liveSessionId, DisplayName: "Aaron", Color: PlayerColor.Red, UserId: userId));

        // Act
        await mediator.Send(new StartLiveSessionCommand(liveSessionId, userId, UserTier.Free, Role.User));

        // Assert — LiveGameSession.CorrelatedGameSessionId is non-null
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var live = await verifyDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveSessionId);
        live.CorrelatedGameSessionId.Should().NotBeNull(
            "StartLiveSessionCommandHandler must set CorrelatedGameSessionId for GameId-backed sessions");

        // Assert — the correlated GameSession row exists with correct fields
        var gsRow = await verifyDb.GameSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(gs => gs.Id == live.CorrelatedGameSessionId!.Value);

        gsRow.Should().NotBeNull("a GameSession row must have been created and committed atomically with the LiveGameSession update");
        gsRow!.CreatedByUserId.Should().Be(userId, "the session creator must be propagated to the GameSession");
        gsRow.GameId.Should().Be(gameId, "GameSession must be linked to the same catalog game");
        gsRow.Status.Should().Be("Setup", "new correlated GameSession starts in Setup status so it counts toward quota");

        // Assert — active count is now 1 (quota-counting + history-visibility)
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var countAfter = await verifyRepo.CountActiveByUserIdAsync(userId);
        countAfter.Should().Be(1, "the newly created correlated GameSession (in Setup status) must count as active");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T2 — Quota enforced: with Free limit overridden to 1, starting a second
    //      GameId-backed session throws QuotaExceededException.
    //
    // Quota tier config approach: IntegrationWebApplicationFactory.Create accepts
    // extraConfig that merges into the in-memory IConfiguration. We set
    // "SessionLimits:free:MaxSessions" = "1" in InitializeAsync so the
    // SessionQuotaService's GetLimitForTierAsync falls through to the configured
    // value instead of the DefaultLimits.FreeMaxSessions (3).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2: Starting beyond Free tier limit throws QuotaExceededException")]
    public async Task T2_Start_BeyondQuota_ThrowsQuotaExceeded()
    {
        // Arrange — seed user + 2 shared games; start the first session (uses 1 of 1 slot)
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId1 = await SeedSharedGameAsync(db);
        var gameId2 = await SeedSharedGameAsync(db);

        var session1Id = await mediator.Send(new CreateLiveSessionCommand(UserId: userId, GameName: "Game 1", GameId: gameId1));
        await mediator.Send(new AddPlayerToLiveSessionCommand(SessionId: session1Id, DisplayName: "Aaron", Color: PlayerColor.Red, UserId: userId));
        await mediator.Send(new StartLiveSessionCommand(session1Id, userId, UserTier.Free, Role.User));

        // Confirm slot is full (active count == 1 == limit)
        await using var checkScope = _factory.Services.CreateAsyncScope();
        var checkRepo = checkScope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var countAfterFirst = await checkRepo.CountActiveByUserIdAsync(userId);
        countAfterFirst.Should().Be(1, "first session consumed the single Free-tier slot");

        // Prepare the second session up to the point of start (create + addPlayer)
        await using var scope2 = _factory.Services.CreateAsyncScope();
        var mediator2 = scope2.ServiceProvider.GetRequiredService<IMediator>();
        var session2Id = await mediator2.Send(new CreateLiveSessionCommand(UserId: userId, GameName: "Game 2", GameId: gameId2));
        await mediator2.Send(new AddPlayerToLiveSessionCommand(SessionId: session2Id, DisplayName: "Aaron", Color: PlayerColor.Blue, UserId: userId));

        // Act — attempt to start beyond the quota limit
        await using var scope3 = _factory.Services.CreateAsyncScope();
        var mediator3 = scope3.ServiceProvider.GetRequiredService<IMediator>();
        Func<Task> act = () => mediator3.Send(new StartLiveSessionCommand(session2Id, userId, UserTier.Free, Role.User));

        // Assert — throws QuotaExceededException (mapped to 409 at the HTTP layer)
        await act.Should().ThrowAsync<QuotaExceededException>(
            "starting a second GameId-backed session when the Free tier limit (1) is already reached " +
            "must be rejected by the quota check in StartLiveSessionCommandHandler");

        // Assert — the second session does NOT have a correlated GameSession (creation was rolled back / never committed)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var live2 = await verifyDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == session2Id);
        live2.CorrelatedGameSessionId.Should().BeNull(
            "the quota check must fire BEFORE the GameSession is created; rolling back leaves CorrelatedGameSessionId null");

        // Active count must still be 1 (not 2)
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var finalCount = await verifyRepo.CountActiveByUserIdAsync(userId);
        finalCount.Should().Be(1, "the failed start must not have created a new active GameSession");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3 — Complete frees the quota: after completing the live session the
    //      correlated GameSession is Completed and CountActiveByUserId drops to 0.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3: Completing live session marks correlated GameSession Completed and frees the quota slot")]
    public async Task T3_Complete_FreesQuota_And_MarksCorrelatedCompleted()
    {
        // Arrange — create + addPlayer + start
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        var liveSessionId = await mediator.Send(new CreateLiveSessionCommand(UserId: userId, GameName: "Wingspan", GameId: gameId));
        await mediator.Send(new AddPlayerToLiveSessionCommand(SessionId: liveSessionId, DisplayName: "Aaron", Color: PlayerColor.Green, UserId: userId));
        await mediator.Send(new StartLiveSessionCommand(liveSessionId, userId, UserTier.Free, Role.User));

        // Confirm slot is consumed
        await using var preScope = _factory.Services.CreateAsyncScope();
        var preRepo = preScope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var countBefore = await preRepo.CountActiveByUserIdAsync(userId);
        countBefore.Should().Be(1, "started session must be counting as active");

        // Act — complete the live session
        await using var completeScope = _factory.Services.CreateAsyncScope();
        var completeMediatorInstance = completeScope.ServiceProvider.GetRequiredService<IMediator>();
        await completeMediatorInstance.Send(new CompleteLiveSessionCommand(liveSessionId));

        // Assert — the correlated GameSession is now Completed (no longer Active)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var live = await verifyDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveSessionId);
        live.CorrelatedGameSessionId.Should().NotBeNull("pre-condition: correlation was set at start");

        var gsRow = await verifyDb.GameSessions
            .AsNoTracking()
            .SingleAsync(gs => gs.Id == live.CorrelatedGameSessionId!.Value);

        gsRow.Status.Should().Be("Completed",
            "CompleteLiveSessionCommandHandler must call GameSession.MarkCorrelatedComplete " +
            "so the shadow record transitions to Completed and stops counting as active");
        gsRow.CompletedAt.Should().NotBeNull("CompletedAt must be stamped by MarkCorrelatedComplete");

        // Assert — active count is back to 0 (slot freed)
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var countAfter = await verifyRepo.CountActiveByUserIdAsync(userId);
        countAfter.Should().Be(0,
            "a Completed GameSession must not be counted by CountActiveByUserIdAsync " +
            "(which filters on Setup / InProgress / Paused statuses only)");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T4 — Free-form (GameId == null): start succeeds but NO GameSession is created
    //      and CorrelatedGameSessionId stays null.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T4: Free-form (GameId=null) start succeeds without creating a correlated GameSession")]
    public async Task T4_Start_FreeForm_NoGameId_DoesNotCreateGameSession()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);

        // Create a free-form session: GameId explicitly null (no catalog game)
        var liveSessionId = await mediator.Send(new CreateLiveSessionCommand(UserId: userId, GameName: "Quick Game", GameId: null));
        await mediator.Send(new AddPlayerToLiveSessionCommand(SessionId: liveSessionId, DisplayName: "Aaron", Color: PlayerColor.Yellow, UserId: userId));

        // Act — start succeeds without quota check (no GameId → no GameSession created)
        Func<Task> act = () => mediator.Send(new StartLiveSessionCommand(liveSessionId, userId, UserTier.Free, Role.User));
        await act.Should().NotThrowAsync("free-form sessions bypass quota and GameSession creation");

        // Assert — CorrelatedGameSessionId is still null
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var live = await verifyDb.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveSessionId);
        live.CorrelatedGameSessionId.Should().BeNull(
            "free-form sessions (GameId == null) must not create or link a correlated GameSession");

        // Assert — NO GameSession row created for this user
        var gsCount = await verifyDb.GameSessions
            .AsNoTracking()
            .CountAsync(gs => gs.CreatedByUserId == userId);

        gsCount.Should().Be(0,
            "no GameSession must be created for a free-form live session; " +
            "only GameId-backed sessions participate in the quota/history saga");

        // Assert — active count stays 0 (free-form doesn't consume a quota slot)
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
        var count = await verifyRepo.CountActiveByUserIdAsync(userId);
        count.Should().Be(0,
            "free-form live sessions do not create a GameSession and therefore never appear in the active count");
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
            Email = $"corr-quota-{userId:N}@test.local",
            DisplayName = "Correlated Quota Test User",
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
            Title = $"Correlation Test Game {gameId:N}",
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

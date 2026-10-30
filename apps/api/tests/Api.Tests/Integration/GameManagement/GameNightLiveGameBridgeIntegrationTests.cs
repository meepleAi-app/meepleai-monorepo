using System.Net;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// La fixture di <see cref="GameNightLiveGameBridgeIntegrationTests"/>: host condiviso per la classe,
/// database fresco per test (#4050). L'avvio di una partita dalla serata passa dal gate di prontezza
/// della KB (<c>kb_not_ready</c>): qui è un doppio SENZA stato che risponde «pronta», perché preparare
/// un PDF indicizzato non è ciò che questi test provano.
///
/// <para>L'avvio lancia anche il «warm-up» del toolbox in background (fire-and-forget, sullo STESSO
/// <c>DbContext</c> dello scope della richiesta): il test chiude lo scope appena il comando ritorna, e
/// quel warm-up in volo faceva fallire la chiusura con un errore di protocollo di Npgsql. Il suo
/// comportamento ha test propri (<c>StartGameNightSessionToolboxWarmupTests</c>): qui la query dei
/// template risponde subito, senza toccare il database.</para>
/// </summary>
public sealed class GameNightLiveGameBridgeIntegrationTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "night_live_bridge")
{
    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IRequestHandler<GetKbReadinessQuery, KbReadinessDto>, AlwaysReadyKbReadinessHandler>();
            services.AddTransient<IRequestHandler<GetToolboxTemplatesQuery, List<ToolboxTemplateDto>>, NoToolboxTemplatesHandler>();
        }));
}

internal sealed class AlwaysReadyKbReadinessHandler : IRequestHandler<GetKbReadinessQuery, KbReadinessDto>
{
    public Task<KbReadinessDto> Handle(GetKbReadinessQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new KbReadinessDto(true, "Ready", 1, 0, Array.Empty<string>()));
}

internal sealed class NoToolboxTemplatesHandler : IRequestHandler<GetToolboxTemplatesQuery, List<ToolboxTemplateDto>>
{
    public Task<List<ToolboxTemplateDto>> Handle(GetToolboxTemplatesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new List<ToolboxTemplateDto>());
}

/// <summary>
/// #4752 (the game night → live game bridge) against a real PostgreSQL: starting a game from a night
/// creates the <c>LiveGameSession</c> in the same transaction — adopting the tracking Session the night
/// links to, seating the night's roster — the night exposes it, go-live gives a draft its live game
/// (idempotently), and the completion follows in both directions.
///
/// <para>Domain events are not dispatched by this host (<c>OutboxOnly</c>, no processor — see
/// <c>integration-host-never-dispatches-domain-events</c>): the night → live direction, which runs as
/// an event handler, is exercised by resolving the real handler from the container and invoking it.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightLiveGameBridgeIntegrationTests
    : IClassFixture<GameNightLiveGameBridgeIntegrationTestsHostFixture>, IAsyncLifetime
{
    private readonly GameNightLiveGameBridgeIntegrationTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public GameNightLiveGameBridgeIntegrationTests(GameNightLiveGameBridgeIntegrationTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Start from a night ───────────────────────────────────────────────────

    [Fact(DisplayName = "Starting a game from a night creates ONE live game on the night's tracking Session, seats the roster, and the night exposes it")]
    public async Task Start_CreatesTheLiveGame_OnTheTrackingSession_WithTheRoster_AndTheNightExposesIt()
    {
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();

        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));

        started.LiveSessionId.Should().NotBeNull("the start must hand back the live game, where the table plays");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var live = await scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>()
            .GetByIdAsync(started.LiveSessionId!.Value);

        live.Should().NotBeNull();
        live!.TrackingSessionId.Should().Be(started.SessionId,
            "the live game adopts the tracking Session the night's link points at");
        live.GameId.Should().Be(gameId);
        live.CreatedByUserId.Should().Be(organizer);
        live.Players.Select(p => p.UserId).Should().BeEquivalentTo(new Guid?[] { organizer, friend, _secondFriend });
        live.Host!.UserId.Should().Be(organizer, "the organizer sits first, and the first seat is the Host");
        // The table order is TurnOrder (the order the seats were added), not the order rows come back.
        live.GetPlayer(live.TurnOrder[0])!.UserId.Should().Be(organizer);
        live.Players.Select(p => p.DisplayName).Should().BeEquivalentTo(
            new[] { "Marta Organizzatrice", "Luca Amico", "Anna Amica" });

        // ONE live game and ONE tracking Session: no second companion was minted for the same night.
        (await db.LiveGameSessions.AsNoTracking().CountAsync(l => l.CreatedByUserId == organizer)).Should().Be(1);
        (await db.SessionTrackingSessions.AsNoTracking().CountAsync(s => s.UserId == organizer)).Should().Be(1);

        var night = await SendAsync(new GetGameNightLiveQuery(nightId, organizer));
        night.Sessions.Should().ContainSingle()
            .Which.LiveSessionId.Should().Be(started.LiveSessionId);
    }

    [Fact(DisplayName = "A second start while a game is live is a 409 and leaves no orphan live game or tracking Session")]
    public async Task Start_WhileAGameIsLive_Conflicts_AndLeavesNoOrphans()
    {
        var (organizer, _, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));

        var act = () => SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Dixit", organizer));

        await act.Should().ThrowAsync<MaxLiveSessionsExceededException>();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.LiveGameSessions.AsNoTracking().CountAsync(l => l.CreatedByUserId == organizer)).Should().Be(1);
        (await db.SessionTrackingSessions.AsNoTracking().CountAsync(s => s.UserId == organizer)).Should().Be(1);
    }

    // ── Go-live of a draft ───────────────────────────────────────────────────

    [Fact(DisplayName = "Go-live gives a draft its live game from the draft's participants, and a second go-live returns the same one")]
    public async Task GoLive_CreatesTheLiveGameFromTheDraft_AndIsIdempotent()
    {
        var (organizer, friend, gameId) = await SeedUsersAndGameAsync();
        var trackingSessionId = await CreateDraftTrackingSessionAsync(organizer, friend, gameId);
        await SeedNightWithPendingDraftAsync(organizer, gameId, trackingSessionId);

        var first = await SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer));
        var second = await SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer));

        first.LiveSessionId.Should().NotBeNull();
        second.LiveSessionId.Should().Be(first.LiveSessionId, "go-live is idempotent: the retry must not mint a second live game");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.LiveGameSessions.AsNoTracking().CountAsync(l => l.TrackingSessionId == trackingSessionId)).Should().Be(1);
        var live = await scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>()
            .GetByIdAsync(first.LiveSessionId!.Value);
        live!.Players.Select(p => p.UserId).Should().BeEquivalentTo(new Guid?[] { organizer, friend });
        live.Host!.UserId.Should().Be(organizer);
    }

    // ── Completion, live → night ─────────────────────────────────────────────

    [Fact(DisplayName = "Completing the live game completes the night's link with the winner the scores decided, and finalizes the tracking Session")]
    public async Task CompletingTheLiveGame_CompletesTheNightLink_WithTheWinner()
    {
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
        await StartTheLiveGameAsync(liveId, organizer);
        await RecordScoresAsync(liveId, (organizer, 3), (friend, 9), (_secondFriend, 1));

        await SendAsync(new CompleteLiveSessionCommand(liveId));
        await SendAsync(new CompleteNightLinkForLiveSessionCommand(liveId));
        // A retry is a no-op: the link is no longer InProgress.
        await SendAsync(new CompleteNightLinkForLiveSessionCommand(liveId));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var link = await db.GameNightSessions.AsNoTracking().SingleAsync(s => s.SessionId == started.SessionId);
        link.Status.Should().Be(nameof(GameNightSessionStatus.Completed));
        var friendParticipant = await db.SessionTrackingParticipants.AsNoTracking()
            .SingleAsync(p => p.SessionId == started.SessionId && p.UserId == friend);
        link.WinnerId.Should().Be(friendParticipant.Id, "the first place of the live game is the night's winner");
        (await db.SessionTrackingSessions.AsNoTracking().SingleAsync(s => s.Id == started.SessionId))
            .FinalizedAt.Should().NotBeNull("the tracking Session must not stay live after the game is over");

        // «Concludi serata» is possible again: nothing is in progress.
        var night = await SendAsync(new GetGameNightLiveQuery(nightId, organizer));
        night.Sessions.Should().ContainSingle().Which.Status.Should().Be(GameNightSessionStatus.Completed);
    }

    [Fact(DisplayName = "POST /live-sessions/{id}/complete — the «Termina partita» endpoint — closes the night's sitting too")]
    public async Task TheCompleteEndpoint_ClosesTheNightSittingToo()
    {
        // The follow-up is a second command the endpoint sends after CompleteLiveSessionCommand. Without
        // the HTTP hop a missing Send would go unseen: every other test sends the two commands itself.
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
        await StartTheLiveGameAsync(liveId, organizer);
        await RecordScoresAsync(liveId, (organizer, 3), (friend, 9), (_secondFriend, 1));

        string token;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, organizer);
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);

        var response = await client.PostAsync(new Uri($"/api/v1/live-sessions/{liveId}/complete", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var verify = _factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await verifyDb.GameNightSessions.AsNoTracking().SingleAsync(s => s.SessionId == started.SessionId))
            .Status.Should().Be(nameof(GameNightSessionStatus.Completed));
    }

    [Fact(DisplayName = "A tie (or nobody scoring) completes the night's link with NO winner — and the finalize accepts the ranks")]
    public async Task CompletingTheLiveGame_WithATie_CompletesTheNightLinkWithoutAWinner()
    {
        // Before #4752 the night finalized "no winner" with every rank equal to 2, which the finalize's own
        // validator rejects (ranks must be 1..N): the night could not be completed at all.
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
        await StartTheLiveGameAsync(liveId, organizer);
        await RecordScoresAsync(liveId, (organizer, 5), (friend, 5), (_secondFriend, 1));

        await SendAsync(new CompleteLiveSessionCommand(liveId));
        await SendAsync(new CompleteNightLinkForLiveSessionCommand(liveId));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var link = await db.GameNightSessions.AsNoTracking().SingleAsync(s => s.SessionId == started.SessionId);
        link.Status.Should().Be(nameof(GameNightSessionStatus.Completed),
            "a tie must not leave the sitting in progress: «Concludi serata» would stay disabled forever");
        link.WinnerId.Should().BeNull("two players share first place: nobody is recorded as the winner");
        (await db.SessionTrackingSessions.AsNoTracking().SingleAsync(s => s.Id == started.SessionId))
            .FinalizedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Completing the game from the night with a winner works for three players (ranks must be 1..N)")]
    public async Task CompletingTheGameFromTheNight_WithAWinner_WorksForThreePlayers()
    {
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        Guid friendParticipant;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            friendParticipant = (await db.SessionTrackingParticipants.AsNoTracking()
                .SingleAsync(p => p.SessionId == started.SessionId && p.UserId == friend)).Id;
        }

        await SendAsync(new CompleteGameNightSessionCommand(nightId, friendParticipant, organizer));

        await using var verify = _factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var link = await verifyDb.GameNightSessions.AsNoTracking().SingleAsync(s => s.SessionId == started.SessionId);
        link.Status.Should().Be(nameof(GameNightSessionStatus.Completed));
        link.WinnerId.Should().Be(friendParticipant);
    }

    // ── Completion, night → live ─────────────────────────────────────────────

    [Fact(DisplayName = "Completing the game from the night completes the STARTED live game, through the event handler")]
    public async Task CompletingTheGameFromTheNight_CompletesTheStartedLiveGame()
    {
        var (organizer, _, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        await StartTheLiveGameAsync(started.LiveSessionId!.Value, organizer);

        await SendAsync(new CompleteGameNightSessionCommand(nightId, null, organizer));
        await PublishNightGameCompletedAsync(nightId, started.SessionId, gameId);

        (await LiveStatusAsync(started.LiveSessionId.Value)).Should().Be(LiveSessionStatus.Completed);
    }

    [Fact(DisplayName = "Completing the game from the night leaves a live game nobody started alone")]
    public async Task CompletingTheGameFromTheNight_LeavesANeverStartedLiveGameAlone()
    {
        var (organizer, _, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));

        await SendAsync(new CompleteGameNightSessionCommand(nightId, null, organizer));
        await PublishNightGameCompletedAsync(nightId, started.SessionId, gameId);

        (await LiveStatusAsync(started.LiveSessionId!.Value)).Should().Be(LiveSessionStatus.Created,
            "a live game nobody started has nothing to record and is not 'active' anywhere");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    // Concrete TRequest on purpose: a variable typed IRequest would resolve the handler of IRequest.
    private async Task SendAsync<TRequest>(TRequest request)
        where TRequest : IRequest
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private Task StartTheLiveGameAsync(Guid liveId, Guid userId) =>
        SendAsync(new StartLiveSessionCommand(liveId, userId, UserTier.Free, Role.User));

    private async Task RecordScoresAsync(Guid liveId, params (Guid UserId, int Score)[] scores)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var live = await scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>().GetByIdAsync(liveId);
        foreach (var (userId, score) in scores)
        {
            var player = live!.Players.Single(p => p.UserId == userId);
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                new RecordLiveSessionScoreCommand(liveId, player.Id, 1, "points", score));
        }
    }

    private async Task<LiveSessionStatus> LiveStatusAsync(Guid liveId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var live = await scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>().GetByIdAsync(liveId);
        return live!.Status;
    }

    /// <summary>
    /// The host never dispatches domain events, so the night → live handler is taken from the container
    /// (which also proves it is registered) and invoked with the event the aggregate raises.
    /// </summary>
    private async Task PublishNightGameCompletedAsync(Guid nightId, Guid trackingSessionId, Guid gameId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetServices<INotificationHandler<GameCompletedInNightEvent>>()
            .OfType<CompleteLiveSessionOnNightGameCompletedHandler>()
            .Should().ContainSingle("the night → live handler must be registered").Subject;

        await handler.Handle(
            new GameCompletedInNightEvent(nightId, trackingSessionId, gameId, "Catan", WinnerId: null),
            CancellationToken.None);
    }

    /// <summary>
    /// A published night with THREE players: the organizer and two accepted friends. Three, not two, on
    /// purpose: the completion ranks the night sends to the finalize (winner 1, the rest 2) were 1..N only
    /// for exactly two participants, so a two-player night would not have seen the defect.
    /// </summary>
    private async Task<(Guid Organizer, Guid Friend, Guid GameId, Guid NightId)> SeedNightWithAcceptedFriendAsync()
    {
        var (organizer, friend, gameId) = await SeedUsersAndGameAsync();
        var nightId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = DateTimeOffset.UtcNow;
        _secondFriend = SeedUser(db, "Anna Amica");
        db.GameNightEvents.Add(NightEntity(nightId, organizer, gameId, now));
        foreach (var guest in new[] { friend, _secondFriend })
        {
            db.GameNightRsvps.Add(new GameNightRsvpEntity
            {
                Id = Guid.NewGuid(),
                EventId = nightId,
                UserId = guest,
                Status = "Accepted",
                RespondedAt = now,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        return (organizer, friend, gameId, nightId);
    }

    private Guid _secondFriend;

    private async Task SeedNightWithPendingDraftAsync(Guid organizer, Guid gameId, Guid trackingSessionId)
    {
        var nightId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.GameNightEvents.Add(NightEntity(nightId, organizer, gameId, DateTimeOffset.UtcNow));
        db.GameNightSessions.Add(new GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = nightId,
            SessionId = trackingSessionId,
            GameId = gameId,
            GameTitle = "Catan",
            PlayOrder = 1,
            Status = nameof(GameNightSessionStatus.Pending),
        });
        await db.SaveChangesAsync();
    }

    private static GameNightEventEntity NightEntity(Guid nightId, Guid organizer, Guid gameId, DateTimeOffset now) => new()
    {
        Id = nightId,
        OrganizerId = organizer,
        Title = "Serata del ponte",
        ScheduledAt = now,
        GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { gameId }),
        Status = nameof(GameNightStatus.Published),
        CreatedAt = now,
        UpdatedAt = now,
    };

    private async Task<Guid> CreateDraftTrackingSessionAsync(Guid organizer, Guid friend, Guid gameId)
    {
        var created = await SendAsync(new CreateSessionCommand(
            UserId: organizer,
            GameId: gameId,
            SessionType: "GameSpecific",
            SessionDate: DateTime.UtcNow,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { Id = Guid.NewGuid(), UserId = friend, DisplayName = "Luca Amico", IsOwner = false, JoinOrder = 1 },
                new() { Id = Guid.NewGuid(), UserId = organizer, DisplayName = "Marta Organizzatrice", IsOwner = true, JoinOrder = 0 },
            },
            SkipGameNightEnvelope: true,
            SkipKbReadinessGate: true));
        return created.SessionId;
    }

    private async Task<(Guid Organizer, Guid Friend, Guid GameId)> SeedUsersAndGameAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var organizer = SeedUser(db, "Marta Organizzatrice");
        var friend = SeedUser(db, "Luca Amico");
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Catan {gameId:N}",
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (organizer, friend, gameId);
    }

    private static Guid SeedUser(MeepleAiDbContext db, string displayName)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"bridge-{userId:N}@test.local",
            DisplayName = displayName,
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
        });
        return userId;
    }
}

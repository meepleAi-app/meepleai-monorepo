using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
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
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
///
/// <para>Il host porta anche due guasti iniettabili (<see cref="TransactionFaultSwitch"/>), per provare su
/// un database vero che l'avvio e il go-live sono atomici: quello che lo switch non arma non cambia nulla.
/// Lo switch vive quanto il host, cioè quanto la classe: ogni test lo riporta a riposo prima di partire.</para>
/// </summary>
public sealed class GameNightLiveGameBridgeIntegrationTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "night_live_bridge")
{
    public TransactionFaultSwitch Faults { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IRequestHandler<GetKbReadinessQuery, KbReadinessDto>, AlwaysReadyKbReadinessHandler>();
            services.AddTransient<IRequestHandler<GetToolboxTemplatesQuery, List<ToolboxTemplateDto>>, NoToolboxTemplatesHandler>();

            services.AddSingleton(Faults);
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(sp => new FaultInjectingUnitOfWork(
                new EfCoreUnitOfWork(sp.GetRequiredService<MeepleAiDbContext>()),
                sp.GetRequiredService<MeepleAiDbContext>(),
                sp.GetRequiredService<TransactionFaultSwitch>()));
            services.RemoveAll<ILiveSessionRepository>();
            services.AddScoped<ILiveSessionRepository>(sp => new FaultInjectingLiveSessionRepository(
                ActivatorUtilities.CreateInstance<LiveSessionRepository>(sp),
                sp.GetRequiredService<TransactionFaultSwitch>()));
        }));
}

/// <summary>Dove lo switch fa fallire la richiesta che sta girando.</summary>
public enum InjectedFaultPoint
{
    None = 0,

    /// <summary>Quando il lavoro della transazione è finito: gli INSERT sono già nel database, il commit no.</summary>
    EndOfTransaction = 1,

    /// <summary>Quando la partita live sta per essere aggiunta: il tracciamento e il collegamento ci sono già.</summary>
    LiveInsert = 2,
}

/// <summary>Un guasto armato scatta UNA volta (la prima che lo incontra) e si disarma da solo.</summary>
public sealed class TransactionFaultSwitch
{
    private int _armed;

    public void Arm(InjectedFaultPoint point) => Interlocked.Exchange(ref _armed, (int)point);

    public void Disarm() => Interlocked.Exchange(ref _armed, 0);

    internal bool TryTrip(InjectedFaultPoint point) =>
        Interlocked.CompareExchange(ref _armed, 0, (int)point) == (int)point;
}

public sealed class InjectedFaultException(InjectedFaultPoint point)
    : Exception($"Injected fault at {point}");

/// <summary>
/// L'unità di lavoro vera, più un guasto a fine lavoro: prima fa arrivare a Postgres ciò che il lavoro ha
/// preparato (altrimenti il rollback non avrebbe niente da togliere), poi lancia DENTRO la transazione.
/// </summary>
internal sealed class FaultInjectingUnitOfWork(
    EfCoreUnitOfWork inner, MeepleAiDbContext db, TransactionFaultSwitch faults) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        inner.SaveChangesAsync(cancellationToken);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) =>
        inner.ExecuteInTransactionAsync(async ct =>
        {
            var result = await work(ct).ConfigureAwait(false);
            await FailIfArmedAsync(ct).ConfigureAwait(false);
            return result;
        }, cancellationToken);

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        inner.ExecuteInTransactionAsync(async ct =>
        {
            await work(ct).ConfigureAwait(false);
            await FailIfArmedAsync(ct).ConfigureAwait(false);
        }, cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.BeginTransactionAsync(cancellationToken);

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.CommitTransactionAsync(cancellationToken);

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.RollbackTransactionAsync(cancellationToken);

    public void Dispose() => inner.Dispose();

    private async Task FailIfArmedAsync(CancellationToken cancellationToken)
    {
        if (!faults.TryTrip(InjectedFaultPoint.EndOfTransaction))
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        throw new InjectedFaultException(InjectedFaultPoint.EndOfTransaction);
    }
}

/// <summary>Il repository vero, più un guasto all'aggiunta della partita live.</summary>
internal sealed class FaultInjectingLiveSessionRepository(
    ILiveSessionRepository inner, TransactionFaultSwitch faults) : ILiveSessionRepository
{
    public Task<LiveGameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.GetByIdAsync(id, cancellationToken);

    public Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default) =>
        inner.GetByCodeAsync(sessionCode, cancellationToken);

    public Task<LiveGameSession?> GetByTrackingSessionIdAsync(
        Guid trackingSessionId, CancellationToken cancellationToken = default) =>
        inner.GetByTrackingSessionIdAsync(trackingSessionId, cancellationToken);

    public Task<IReadOnlyList<LiveGameSession>> GetActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        inner.GetActiveByUserIdAsync(userId, cancellationToken);

    public Task<Guid?> GetActiveInProgressSessionIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        inner.GetActiveInProgressSessionIdAsync(userId, cancellationToken);

    public Task<IReadOnlyList<LiveGameSession>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        inner.GetAllActiveAsync(cancellationToken);

    public Task AddAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        if (faults.TryTrip(InjectedFaultPoint.LiveInsert))
        {
            throw new InjectedFaultException(InjectedFaultPoint.LiveInsert);
        }

        return inner.AddAsync(session, cancellationToken);
    }

    public Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(session, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.ExistsAsync(id, cancellationToken);
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
        // The host (and its switch) outlives the test: never inherit an armed fault from a previous one.
        _hostFixture.Faults.Disarm();
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
        live.Status.Should().Be(LiveSessionStatus.InProgress,
            "nothing in the front-end starts a live game: it has to be born playable, or scoring and «Termina partita» answer 409");
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
        live.Status.Should().Be(LiveSessionStatus.InProgress, "going live hands over a game that can be played");
    }

    // ── Completion, live → night ─────────────────────────────────────────────

    [Fact(DisplayName = "Completing the live game completes the night's link with the winner the scores decided, and finalizes the tracking Session")]
    public async Task CompletingTheLiveGame_CompletesTheNightLink_WithTheWinner()
    {
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
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

    [Fact(DisplayName = "A game started from a night is scored and ended over HTTP with no other start, and «Termina partita» closes the night's sitting too")]
    public async Task ScoringAndEndingOverHttp_WorkOnAGameStartedFromTheNight_AndCloseTheNightSitting()
    {
        // The only start of this live game is the night's own: nothing else in the app starts one. Before
        // the live game was born InProgress, both calls below answered 409. The follow-up of «Termina
        // partita» is a second command the endpoint sends: without the HTTP hop a missing Send would go
        // unseen, because every other test sends the two commands itself.
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
        var seats = await SeatsByUserAsync(liveId);

        using var client = await ClientForAsync(organizer);
        foreach (var (userId, value) in new[] { (organizer, 3), (friend, 9), (_secondFriend, 1) })
        {
            var scored = await client.PostAsJsonAsync(
                new Uri($"/api/v1/live-sessions/{liveId}/scores", UriKind.Relative),
                new { playerId = seats[userId], round = 1, dimension = "points", value });
            scored.StatusCode.Should().Be(HttpStatusCode.NoContent, "a game started from the night must accept scores at once");
        }

        var response = await client.PostAsync(new Uri($"/api/v1/live-sessions/{liveId}/complete", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "a game started from the night must be endable at once");
        await using var verify = _factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var link = await verifyDb.GameNightSessions.AsNoTracking().SingleAsync(s => s.SessionId == started.SessionId);
        link.Status.Should().Be(nameof(GameNightSessionStatus.Completed));
        var friendParticipant = await verifyDb.SessionTrackingParticipants.AsNoTracking()
            .SingleAsync(p => p.SessionId == started.SessionId && p.UserId == friend);
        link.WinnerId.Should().Be(friendParticipant.Id);
    }

    [Fact(DisplayName = "A repeated «Termina partita» still answers 409 but repairs a night whose sitting was left open")]
    public async Task TheCompleteEndpoint_Repeated_RepairsAnOpenNightLink_AndStillAnswers409()
    {
        // The first «Termina partita» can complete the live game and then fail to close the night's sitting
        // (the follow-up is best-effort). The player taps again: the live game is already over, so the answer
        // is the 409 it always was — but the sitting stays InProgress forever unless this retry closes it,
        // and with it «Concludi serata» and «Aggiungi partita» stay off.
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
        await RecordScoresAsync(liveId, (organizer, 3), (friend, 9), (_secondFriend, 1));
        await SendAsync(new CompleteLiveSessionCommand(liveId)); // the game is over; the follow-up never ran
        (await LinkStatusAsync(started.SessionId)).Should().Be(nameof(GameNightSessionStatus.InProgress),
            "precondition: the sitting was left open");

        using var client = await ClientForAsync(organizer);
        var response = await client.PostAsync(new Uri($"/api/v1/live-sessions/{liveId}/complete", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "the live game was already completed");
        (await LinkStatusAsync(started.SessionId)).Should().Be(nameof(GameNightSessionStatus.Completed),
            "the repeat must close the sitting the first attempt left open");
    }

    [Fact(DisplayName = "A tie (or nobody scoring) completes the night's link with NO winner — and the finalize accepts the ranks")]
    public async Task CompletingTheLiveGame_WithATie_CompletesTheNightLinkWithoutAWinner()
    {
        // Before #4752 the night finalized "no winner" with every rank equal to 2, which the finalize's own
        // validator rejects (ranks must be 1..N): the night could not be completed at all.
        var (organizer, friend, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        var started = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));
        var liveId = started.LiveSessionId!.Value;
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
        (await LiveStatusAsync(started.LiveSessionId!.Value)).Should().Be(LiveSessionStatus.InProgress,
            "precondition: the start from the night is the only start");

        await SendAsync(new CompleteGameNightSessionCommand(nightId, null, organizer));
        await PublishNightGameCompletedAsync(nightId, started.SessionId, gameId);

        (await LiveStatusAsync(started.LiveSessionId.Value)).Should().Be(LiveSessionStatus.Completed);
    }

    // ── Atomicity (a real database, an injected failure) ─────────────────────

    [Theory(DisplayName = "A start that fails after the inserts leaves no tracking Session, no link and no live game — and can be retried")]
    [InlineData(InjectedFaultPoint.EndOfTransaction)]
    [InlineData(InjectedFaultPoint.LiveInsert)]
    public async Task Start_WhenItFailsPartWay_LeavesNothingBehind_AndCanBeRetried(InjectedFaultPoint point)
    {
        // EndOfTransaction: everything is written (tracking Session, link, live game) and the commit never
        // comes. LiveInsert: the tracking Session and the link are written and the live game is refused —
        // the half-made state that a live game created in ITS OWN transaction, after the commit, would leave.
        var (organizer, _, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();
        _hostFixture.Faults.Arm(point);

        var act = () => SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));

        (await act.Should().ThrowAsync<InjectedFaultException>()).Which.Message.Should().Contain(point.ToString());
        (await NightStateAsync(organizer, nightId)).Should().Be(
            (Tracking: 0, Links: 0, Lives: 0),
            "the failed start must leave the night exactly as it found it");

        var retried = await SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer));

        retried.LiveSessionId.Should().NotBeNull();
        (await NightStateAsync(organizer, nightId)).Should().Be((Tracking: 1, Links: 1, Lives: 1));
    }

    [Theory(DisplayName = "A go-live that fails after the inserts leaves the draft pending with no live game — and can be retried")]
    [InlineData(InjectedFaultPoint.EndOfTransaction)]
    [InlineData(InjectedFaultPoint.LiveInsert)]
    public async Task GoLive_WhenItFailsPartWay_LeavesTheDraftPending_AndCanBeRetried(InjectedFaultPoint point)
    {
        // The promotion (link Pending → InProgress) and the live game share one transaction. Were the
        // promotion committed alone, a failed live game would leave a link InProgress with no live game
        // behind it, and the next go-live would read it InProgress and take the repair branch.
        var (organizer, friend, gameId) = await SeedUsersAndGameAsync();
        var trackingSessionId = await CreateDraftTrackingSessionAsync(organizer, friend, gameId);
        await SeedNightWithPendingDraftAsync(organizer, gameId, trackingSessionId);
        _hostFixture.Faults.Arm(point);

        var act = () => SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer));

        (await act.Should().ThrowAsync<InjectedFaultException>()).Which.Message.Should().Contain(point.ToString());
        (await LinkStatusAsync(trackingSessionId)).Should().Be(nameof(GameNightSessionStatus.Pending),
            "the promotion must roll back with the live game");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (await db.LiveGameSessions.AsNoTracking().CountAsync(l => l.TrackingSessionId == trackingSessionId)).Should().Be(0);
        }

        var retried = await SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer));

        retried.LiveSessionId.Should().NotBeNull();
        (await LinkStatusAsync(trackingSessionId)).Should().Be(nameof(GameNightSessionStatus.InProgress));
        (await LiveStatusAsync(retried.LiveSessionId!.Value)).Should().Be(LiveSessionStatus.InProgress);
    }

    // ── Concurrency ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Two starts of the same night at once: exactly one tracking Session, link and live game")]
    public async Task TwoConcurrentStarts_LeaveExactlyOneOfEach()
    {
        var (organizer, _, gameId, nightId) = await SeedNightWithAcceptedFriendAsync();

        var attempts = await Task.WhenAll(
            AttemptAsync(() => SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer))),
            AttemptAsync(() => SendAsync(new StartGameNightSessionCommand(nightId, gameId, "Catan", organizer))));

        attempts.Count(a => a.Error is null).Should().Be(1, "only one of two concurrent starts may win");
        // The loser is told «a game is already live» — or, in the narrow window where both pass the guard
        // and the second's INSERT meets the unique index before the xmin check (a known gap: the start has
        // no 23505 catch, tracked as a follow-up), a DbUpdateException. Never a second game.
        attempts.Where(a => a.Error is not null).Select(a => a.Error)
            .Should().AllSatisfy(e => e.Should().Match<Exception>(x =>
                x is MaxLiveSessionsExceededException || x is DbUpdateException || x is ConflictException));
        (await NightStateAsync(organizer, nightId)).Should().Be((Tracking: 1, Links: 1, Lives: 1));
    }

    [Fact(DisplayName = "Two go-lives of the same draft at once: one live game, and every success returns it")]
    public async Task TwoConcurrentGoLives_LeaveOneLiveGame_AndEverySuccessReturnsIt()
    {
        // Promotion and live game used to commit separately: a second go-live that read the link InProgress
        // between the two took the repair branch and created a SECOND live game for the same draft.
        var (organizer, friend, gameId) = await SeedUsersAndGameAsync();
        var trackingSessionId = await CreateDraftTrackingSessionAsync(organizer, friend, gameId);
        await SeedNightWithPendingDraftAsync(organizer, gameId, trackingSessionId);

        var attempts = await Task.WhenAll(
            AttemptAsync(() => SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer))),
            AttemptAsync(() => SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer))),
            AttemptAsync(() => SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer))));

        var successes = attempts.Where(a => a.Error is null).Select(a => a.Result!.LiveSessionId).ToList();
        successes.Should().NotBeEmpty("at least one go-live must win");
        successes.Distinct().Should().ContainSingle("every successful go-live must hand back the same live game");
        attempts.Where(a => a.Error is not null).Select(a => a.Error)
            .Should().AllSatisfy(e => e.Should().Match<Exception>(x =>
                x is MaxLiveSessionsExceededException || x is DbUpdateException || x is ConflictException));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.LiveGameSessions.AsNoTracking().CountAsync(l => l.TrackingSessionId == trackingSessionId)).Should().Be(1);
    }

    // ── Who may adopt a tracking Session ─────────────────────────────────────

    [Fact(DisplayName = "Somebody who neither owns the tracking Session nor organizes its night cannot adopt it into a live game")]
    public async Task CreateLive_AdoptingSomeoneElsesTrackingSession_IsForbidden()
    {
        var (organizer, friend, gameId) = await SeedUsersAndGameAsync();
        var trackingSessionId = await CreateDraftTrackingSessionAsync(organizer, friend, gameId);
        Guid stranger;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            stranger = SeedUser(db, "Estraneo Curioso");
            await db.SaveChangesAsync();
        }

        var act = () => SendAsync(new CreateLiveSessionCommand(
            stranger, "Catan", gameId, ExistingTrackingSessionId: trackingSessionId));

        await act.Should().ThrowAsync<ForbiddenException>();
        await using var verify = _factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await verifyDb.LiveGameSessions.AsNoTracking().CountAsync(l => l.TrackingSessionId == trackingSessionId))
            .Should().Be(0, "a refused adoption must not leave a live game on somebody else's Session");
    }

    [Fact(DisplayName = "The night's organizer can go live on a draft somebody else planned")]
    public async Task GoLive_ByTheNightsOrganizer_OnADraftAnotherUserPlanned_Works()
    {
        // The tracking Session belongs to its planner, the night to its organizer: going live is the
        // organizer's act, so the ownership check must accept the night's organizer too.
        var (organizer, planner, gameId) = await SeedUsersAndGameAsync();
        var trackingSessionId = await CreateDraftTrackingSessionAsync(planner, organizer, gameId);
        await SeedNightWithPendingDraftAsync(organizer, gameId, trackingSessionId);

        var result = await SendAsync(new GoLiveSessionCommand(trackingSessionId, organizer));

        result.LiveSessionId.Should().NotBeNull();
        await using var scope = _factory.Services.CreateAsyncScope();
        var live = await scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>()
            .GetByIdAsync(result.LiveSessionId!.Value);
        live!.TrackingSessionId.Should().Be(trackingSessionId);
        live.CreatedByUserId.Should().Be(organizer);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(T? Result, Exception? Error)> AttemptAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return (await call(), null);
        }
        catch (Exception ex)
        {
            return (default, ex);
        }
    }

    private async Task<HttpClient> ClientForAsync(Guid userId)
    {
        string token;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);
        return client;
    }

    /// <summary>The seat (player id) of each User at the live game's table.</summary>
    private async Task<Dictionary<Guid, Guid>> SeatsByUserAsync(Guid liveId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var live = await scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>().GetByIdAsync(liveId);
        return live!.Players.Where(p => p.UserId.HasValue).ToDictionary(p => p.UserId!.Value, p => p.Id);
    }

    private async Task<string> LinkStatusAsync(Guid trackingSessionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return (await db.GameNightSessions.AsNoTracking().SingleAsync(s => s.SessionId == trackingSessionId)).Status;
    }

    /// <summary>What a start from <paramref name="nightId"/> leaves behind, counted from the database.</summary>
    /// <remarks>
    /// The night's own status is not counted: Published → InProgress is the job of a domain event, and this
    /// host dispatches none.
    /// </remarks>
    private async Task<(int Tracking, int Links, int Lives)> NightStateAsync(Guid organizer, Guid nightId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return (
            await db.SessionTrackingSessions.AsNoTracking().CountAsync(s => s.UserId == organizer),
            await db.GameNightSessions.AsNoTracking().CountAsync(s => s.GameNightEventId == nightId),
            await db.LiveGameSessions.AsNoTracking().CountAsync(l => l.CreatedByUserId == organizer));
    }

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

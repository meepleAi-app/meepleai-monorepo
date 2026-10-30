using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// #4752 (the game night → live game bridge): <see cref="CreateLiveSessionCommandHandler"/> when the
/// caller — a game night — already owns the tracking Session and brings its roster. The creation stays
/// ONE funnel (ADR-083): the same handler, with the companion and the first seats supplied by the caller.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateLiveSessionForGameNightTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICompanionSessionService> _companion = new();
    private readonly Mock<IGameNightEventRepository> _nights = new();
    private readonly ListLogger<CreateLiveSessionCommandHandler> _logger = new();
    private readonly CreateLiveSessionCommandHandler _sut;
    private readonly Guid _caller = Guid.NewGuid();
    private LiveGameSession? _added;

    public CreateLiveSessionForGameNightTests()
    {
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => _added = s)
            .Returns(Task.CompletedTask);
        // By default the caller owns the tracking Session (what the night start has just created): the tests
        // about WHO may adopt one turn this off.
        _companion.Setup(c => c.IsOwnedByAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sut = new CreateLiveSessionCommandHandler(
            _sessionRepo.Object, TimeProvider.System, _uow.Object, _companion.Object, _nights.Object, _logger);
    }

    private CreateLiveSessionCommand NightCommand(
        Guid trackingSessionId, params LiveSessionInitialPlayer[] players) =>
        new(UserId: _caller,
            GameName: "Catan",
            GameId: Guid.NewGuid(),
            ExistingTrackingSessionId: trackingSessionId,
            InitialPlayers: players);

    private CreateLiveSessionCommand StartedNightCommand(
        Guid trackingSessionId, params LiveSessionInitialPlayer[] players) =>
        NightCommand(trackingSessionId, players) with { StartImmediately = true };

    /// <summary>Captures what the handler logs, with the rendered message (the counts, never the names).</summary>
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task Handle_WithExistingTrackingSession_AdoptsItAndCreatesNoSecondCompanion()
    {
        var trackingId = Guid.NewGuid();

        await _sut.Handle(NightCommand(trackingId), CancellationToken.None);

        _added.Should().NotBeNull();
        _added!.TrackingSessionId.Should().Be(trackingId);
        _companion.Verify(
            c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenALiveSessionAlreadyOwnsTheTrackingSession_ReturnsItAndCreatesNothing()
    {
        // A retry of the start, or a go-live after a start that already made the live game, must get
        // the same live game back — not a second one on the same companion.
        var trackingId = Guid.NewGuid();
        var existing = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System, Guid.NewGuid(),
            trackingSessionId: trackingId);
        _sessionRepo.Setup(r => r.GetByTrackingSessionIdAsync(trackingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var id = await _sut.Handle(NightCommand(trackingId, new LiveSessionInitialPlayer(Guid.NewGuid(), "Anna")), CancellationToken.None);

        id.Should().Be(existing.Id);
        _sessionRepo.Verify(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SeatsTheRosterInOrder_TheFirstSeatIsTheHostAndKeepsItsUser()
    {
        var organizer = Guid.NewGuid();
        var guest = Guid.NewGuid();

        await _sut.Handle(
            NightCommand(
                Guid.NewGuid(),
                new LiveSessionInitialPlayer(organizer, "Marta"),
                new LiveSessionInitialPlayer(guest, "Luca")),
            CancellationToken.None);

        _added!.Players.Select(p => p.DisplayName).Should().Equal("Marta", "Luca");
        _added.Players.Select(p => p.UserId).Should().Equal(organizer, guest);
        _added.Players[0].Role.Should().Be(PlayerRole.Host);
        _added.Players[1].Role.Should().Be(PlayerRole.Player);
        _added.Host!.UserId.Should().Be(organizer);
    }

    [Fact]
    public async Task Handle_GivesEverySeatItsOwnColour()
    {
        await _sut.Handle(
            NightCommand(
                Guid.NewGuid(),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "A"),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "B"),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "C")),
            CancellationToken.None);

        _added!.Players.Select(p => p.Color).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Handle_TwoPlayersWithTheSameNameDifferingOnlyInCase_GetNumericSuffixInsteadOfFailingTheStart()
    {
        // The domain rejects duplicate names (trim + invariant lowercase). A night roster can hold two
        // users with the same display name: the start must not fail for that.
        await _sut.Handle(
            NightCommand(
                Guid.NewGuid(),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "Anna"),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "anna "),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "ANNA")),
            CancellationToken.None);

        _added!.Players.Select(p => p.DisplayName).Should().Equal("Anna", "anna (2)", "ANNA (3)");
    }

    [Fact]
    public async Task Handle_ASuffixThatCollidesWithARealName_KeepsCountingUntilFree()
    {
        await _sut.Handle(
            NightCommand(
                Guid.NewGuid(),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "Anna"),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "Anna (2)"),
                new LiveSessionInitialPlayer(Guid.NewGuid(), "Anna")),
            CancellationToken.None);

        _added!.Players.Select(p => p.DisplayName).Should().Equal("Anna", "Anna (2)", "Anna (3)");
    }

    [Fact]
    public async Task Handle_ABlankName_FallsBackToADefaultInsteadOfFailingTheStart()
    {
        await _sut.Handle(
            NightCommand(Guid.NewGuid(), new LiveSessionInitialPlayer(Guid.NewGuid(), "  ")),
            CancellationToken.None);

        _added!.Players.Should().ContainSingle().Which.DisplayName.Should().Be("Giocatore");
    }

    [Fact]
    public async Task Handle_ALongNameWithASuffix_StaysWithinTheNameLimit()
    {
        var longName = new string('x', 100);

        await _sut.Handle(
            NightCommand(
                Guid.NewGuid(),
                new LiveSessionInitialPlayer(Guid.NewGuid(), longName),
                new LiveSessionInitialPlayer(Guid.NewGuid(), longName)),
            CancellationToken.None);

        _added!.Players.Should().HaveCount(2);
        _added.Players.Select(p => p.DisplayName.Length).Should().OnlyContain(length => length <= 100);
        _added.Players[1].DisplayName.Should().EndWith(" (2)");
    }

    [Fact]
    public async Task Handle_TheSameUserTwice_SeatsThemOnce()
    {
        var user = Guid.NewGuid();

        await _sut.Handle(
            NightCommand(
                Guid.NewGuid(),
                new LiveSessionInitialPlayer(user, "Marta"),
                new LiveSessionInitialPlayer(user, "Marta bis")),
            CancellationToken.None);

        _added!.Players.Should().ContainSingle().Which.DisplayName.Should().Be("Marta");
    }

    [Fact]
    public async Task Handle_ARosterLongerThanThePalette_IsTruncatedInsteadOfFailingTheStart()
    {
        var palette = Enum.GetValues<PlayerColor>().Length;
        var players = Enumerable.Range(1, palette + 3)
            .Select(i => new LiveSessionInitialPlayer(Guid.NewGuid(), $"P{i}"))
            .ToArray();

        await _sut.Handle(NightCommand(Guid.NewGuid(), players), CancellationToken.None);

        _added!.Players.Should().HaveCount(palette);
        _added.Players.Select(p => p.DisplayName).Should().Equal(players.Take(palette).Select(p => p.DisplayName));
    }

    [Fact]
    public async Task Handle_ARosterLongerThanThePalette_SaysSoInTheLog_WithCountsAndNoNames()
    {
        // The truncation is silent for the player (the others can still be added by hand later): the log is the
        // one place that says a roster was cut. The counts, and no personal data.
        var palette = Enum.GetValues<PlayerColor>().Length;
        var players = Enumerable.Range(1, palette + 3)
            .Select(i => new LiveSessionInitialPlayer(Guid.NewGuid(), $"Giocatore-segreto-{i}"))
            .ToArray();

        await _sut.Handle(NightCommand(Guid.NewGuid(), players), CancellationToken.None);

        var warning = _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject.Message;
        warning.Should().Contain((palette + 3).ToString(System.Globalization.CultureInfo.InvariantCulture));
        warning.Should().Contain("3 left out");
        warning.Should().NotContain("Giocatore-segreto");
    }

    [Fact]
    public async Task Handle_ARosterThatFits_LogsNoWarning()
    {
        await _sut.Handle(
            NightCommand(Guid.NewGuid(), new LiveSessionInitialPlayer(Guid.NewGuid(), "Anna")),
            CancellationToken.None);

        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_WithoutInitialPlayers_StillCreatesAnEmptyLiveGame()
    {
        await _sut.Handle(NightCommand(Guid.NewGuid()), CancellationToken.None);

        _added!.Players.Should().BeEmpty();
    }

    // ── StartImmediately (#4752): a game started from a night is about to be played ──────────────────────

    [Fact]
    public async Task Handle_StartImmediately_StartsTheLiveGame_SoScoresTurnsAndCompletionAreOpen()
    {
        // Left Created, scores, turns and «Termina partita» answer 409 — and nothing in the front-end starts a
        // live game. It is started in the same commit as its creation.
        await _sut.Handle(
            StartedNightCommand(Guid.NewGuid(), new LiveSessionInitialPlayer(Guid.NewGuid(), "Marta")),
            CancellationToken.None);

        _added!.Status.Should().Be(LiveSessionStatus.InProgress);
        _added.StartedAt.Should().NotBeNull();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StartImmediately_OfARosterGame_LetsTheHostRecordAScore()
    {
        var host = Guid.NewGuid();
        await _sut.Handle(
            StartedNightCommand(Guid.NewGuid(), new LiveSessionInitialPlayer(host, "Marta")),
            CancellationToken.None);

        var act = () => _added!.RecordScore(_added.Host!.Id, 1, "points", 5);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Handle_WithoutStartImmediately_LeavesTheLiveGameCreated()
    {
        await _sut.Handle(
            NightCommand(Guid.NewGuid(), new LiveSessionInitialPlayer(Guid.NewGuid(), "Marta")),
            CancellationToken.None);

        _added!.Status.Should().Be(LiveSessionStatus.Created);
    }

    [Fact]
    public async Task Handle_StartImmediately_WithNoPlayers_StaysCreated_AndSaysSo()
    {
        // The domain refuses to start a game with nobody at the table: it stays Created (the night handlers
        // always seat somebody, so this is a defensive path), and the log says why.
        await _sut.Handle(StartedNightCommand(Guid.NewGuid()), CancellationToken.None);

        _added!.Status.Should().Be(LiveSessionStatus.Created);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message.Contains("stays Created"));
    }

    // ── Who may adopt a tracking Session (#4752, defence in depth) ───────────────────────────────────────

    [Fact]
    public async Task Handle_TheCallerOwnsTheTrackingSession_MayAdoptIt()
    {
        var trackingId = Guid.NewGuid();

        await _sut.Handle(NightCommand(trackingId), CancellationToken.None);

        _companion.Verify(c => c.IsOwnedByAsync(trackingId, _caller, It.IsAny<CancellationToken>()), Times.Once);
        _added!.TrackingSessionId.Should().Be(trackingId);
    }

    [Fact]
    public async Task Handle_TheCallerOrganizesTheNightTheSessionBelongsTo_MayAdoptADraftSomeoneElsePlanned()
    {
        // A go-live of a draft that another participant planned: the tracking Session is theirs, the night is the
        // caller's.
        var trackingId = Guid.NewGuid();
        _companion.Setup(c => c.IsOwnedByAsync(trackingId, _caller, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _nights.Setup(r => r.GetByLinkedSessionIdAsync(trackingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameNightEvent.Create(_caller, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]));

        await _sut.Handle(NightCommand(trackingId), CancellationToken.None);

        _added!.TrackingSessionId.Should().Be(trackingId);
    }

    [Fact]
    public async Task Handle_ATrackingSessionThatIsNotTheCallersAndNotOfTheirNight_IsRefused_AndNothingIsCreated()
    {
        var trackingId = Guid.NewGuid();
        _companion.Setup(c => c.IsOwnedByAsync(trackingId, _caller, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _nights.Setup(r => r.GetByLinkedSessionIdAsync(trackingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameNightEvent.Create(Guid.NewGuid(), "Serata di un altro", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]));

        var act = () => _sut.Handle(
            NightCommand(trackingId, new LiveSessionInitialPlayer(Guid.NewGuid(), "Marta")), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _sessionRepo.Verify(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ATrackingSessionBelongingToNoNightAndNotTheCallers_IsRefused()
    {
        var trackingId = Guid.NewGuid();
        _companion.Setup(c => c.IsOwnedByAsync(trackingId, _caller, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _nights.Setup(r => r.GetByLinkedSessionIdAsync(trackingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var act = () => _sut.Handle(NightCommand(trackingId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_TheIdempotentAnswerIsGuardedToo_ACallerWhoMayNotAdoptDoesNotGetSomeoneElsesLiveGame()
    {
        // Without the check first, asking for the live game of ANY tracking id would hand it back.
        var trackingId = Guid.NewGuid();
        var existing = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System, Guid.NewGuid(), trackingSessionId: trackingId);
        _sessionRepo.Setup(r => r.GetByTrackingSessionIdAsync(trackingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _companion.Setup(c => c.IsOwnedByAsync(trackingId, _caller, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => _sut.Handle(NightCommand(trackingId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_WithoutAnExistingTrackingSession_DoesNotRunTheOwnershipCheck()
    {
        // The ordinary creation makes its own companion: there is nothing to adopt, so nothing to check.
        _companion.Setup(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _sut.Handle(
            new CreateLiveSessionCommand(UserId: _caller, GameName: "Catan", GameId: Guid.NewGuid()),
            CancellationToken.None);

        _companion.Verify(
            c => c.IsOwnedByAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _nights.Verify(
            r => r.GetByLinkedSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

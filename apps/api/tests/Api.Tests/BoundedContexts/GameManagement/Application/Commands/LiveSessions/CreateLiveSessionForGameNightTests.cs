using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
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
    private readonly CreateLiveSessionCommandHandler _sut;
    private LiveGameSession? _added;

    public CreateLiveSessionForGameNightTests()
    {
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => _added = s)
            .Returns(Task.CompletedTask);
        _sut = new CreateLiveSessionCommandHandler(_sessionRepo.Object, TimeProvider.System, _uow.Object, _companion.Object);
    }

    private static CreateLiveSessionCommand NightCommand(
        Guid trackingSessionId, params LiveSessionInitialPlayer[] players) =>
        new(UserId: Guid.NewGuid(),
            GameName: "Catan",
            GameId: Guid.NewGuid(),
            ExistingTrackingSessionId: trackingSessionId,
            InitialPlayers: players);

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
    public async Task Handle_WithoutInitialPlayers_StillCreatesAnEmptyLiveGame()
    {
        await _sut.Handle(NightCommand(Guid.NewGuid()), CancellationToken.None);

        _added!.Players.Should().BeEmpty();
    }
}

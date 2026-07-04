using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2633 C2: the GameManagement night-diary read is now participant-scoped (404 missing / 403
/// non-participant, parity with the live query) and reads the RECENT window newest-first, then
/// re-sorts to chronological. (The colliding SessionFlow twin route was retired.)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightDiaryQueryHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _gameNightRepo = new();
    private readonly Mock<ISessionEventRepository> _eventRepo = new();
    private readonly GetGameNightDiaryQueryHandler _handler;

    public GetGameNightDiaryQueryHandlerTests()
    {
        _handler = new GetGameNightDiaryQueryHandler(_gameNightRepo.Object, _eventRepo.Object);
        // Default: no events unless a test sets some.
        _eventRepo
            .Setup(r => r.GetByGameNightIdAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SessionEvent>());
    }

    private static GameNightEvent PublishedNight(Guid? organizerId = null, List<Guid>? invited = null)
    {
        var evt = GameNightEvent.Create(
            organizerId ?? Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid()]);
        evt.Publish(invited ?? []);
        return evt;
    }

    private static SessionEvent EventAt(Guid gameNightId, string type, DateTime timestamp)
    {
        var e = SessionEvent.Create(Guid.NewGuid(), type, gameNightId: gameNightId);
        // Timestamp has a private setter (factory pins it to UtcNow) — set it explicitly the same
        // way the persistence mappers hydrate domain entities.
        typeof(SessionEvent).GetProperty(nameof(SessionEvent.Timestamp))!.SetValue(e, timestamp);
        return e;
    }

    [Fact]
    public async Task Handle_MissingNight_ThrowsNotFound()
    {
        _gameNightRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var act = () => _handler.Handle(
            new GetGameNightDiaryQuery(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        var evt = PublishedNight();
        _gameNightRepo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var act = () => _handler.Handle(
            new GetGameNightDiaryQuery(evt.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NonParticipant_NeverReadsTheDiary()
    {
        var evt = PublishedNight();
        _gameNightRepo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        await _handler.Invoking(h => h.Handle(
                new GetGameNightDiaryQuery(evt.Id, Guid.NewGuid()), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ForbiddenException>();

        _eventRepo.Verify(r => r.GetByGameNightIdAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Organizer_ReturnsDiaryEnvelope()
    {
        var evt = PublishedNight();
        _gameNightRepo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightDiaryQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        result.GameNightId.Should().Be(evt.Id);
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvitedRsvpPlayer_IsAllowed()
    {
        var invited = Guid.NewGuid();
        var evt = PublishedNight(invited: [invited]);
        _gameNightRepo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var act = () => _handler.Handle(
            new GetGameNightDiaryQuery(evt.Id, invited), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ReadsRecentWindowNewestFirst_ButReturnsChronological()
    {
        var evt = PublishedNight();
        _gameNightRepo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var t1 = new DateTime(2026, 7, 4, 20, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 7, 4, 20, 1, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 7, 4, 20, 2, 0, DateTimeKind.Utc);
        // Repo returns newest-first (as newestFirst:true would): t3, t2, t1.
        _eventRepo
            .Setup(r => r.GetByGameNightIdAsync(evt.Id, It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { EventAt(evt.Id, "score_updated", t3), EventAt(evt.Id, "turn_advanced", t2), EventAt(evt.Id, "game_started", t1) });

        var result = await _handler.Handle(
            new GetGameNightDiaryQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        // Handler re-sorts to chronological ASC for render.
        result.Entries.Select(e => e.Timestamp).Should().ContainInOrder(t1, t2, t3);
        // Recency contract: it must have requested the newest-first window.
        _eventRepo.Verify(r => r.GetByGameNightIdAsync(
            evt.Id, It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GeneratesItalianDescription_ForKnownEventType()
    {
        var evt = PublishedNight();
        _gameNightRepo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);
        _eventRepo
            .Setup(r => r.GetByGameNightIdAsync(evt.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { EventAt(evt.Id, "game_started", DateTime.UtcNow) });

        var result = await _handler.Handle(
            new GetGameNightDiaryQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        result.Entries.Should().ContainSingle();
        result.Entries[0].EventType.Should().Be("game_started");
        result.Entries[0].Description.Should().Be("🎲 Partita iniziata");
    }
}

using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2633 Slice A: the night-live read model projects the night header + its session progression
/// (status/order/winner/timing) so the FE can wire NightLiveClientView off fixtures.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightLiveQueryHandlerTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetGameNightLiveQueryHandler _handler;

    public GetGameNightLiveQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetGameNightLiveQueryHandler(_repo.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_ProjectsSessions_OrderedByPlayOrderWithStatuses()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata da Marco", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        evt.AddSession(s1, evt.GameIds[0], "Brass: Birmingham"); // PlayOrder 1
        evt.AddSession(s2, evt.GameIds[1], "Spirit Island"); // PlayOrder 2
        evt.StartCurrentSession(); // s1 → InProgress
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        result.Id.Should().Be(evt.Id);
        result.Title.Should().Be("Serata da Marco");
        result.Status.Should().Be(GameNightStatus.Published);
        result.Sessions.Should().HaveCount(2);

        result.Sessions[0].PlayOrder.Should().Be(1);
        result.Sessions[0].SessionId.Should().Be(s1);
        result.Sessions[0].GameTitle.Should().Be("Brass: Birmingham");
        result.Sessions[0].Status.Should().Be(GameNightSessionStatus.InProgress);
        result.Sessions[0].StartedAt.Should().NotBeNull();

        result.Sessions[1].PlayOrder.Should().Be(2);
        result.Sessions[1].GameTitle.Should().Be("Spirit Island");
        result.Sessions[1].Status.Should().Be(GameNightSessionStatus.Pending);
        result.Sessions[1].StartedAt.Should().BeNull();
    }

    // WS1 DEC-9: the read model exposes whether the viewer is the organizer, so the FE
    // renders the organizer-only "Avvia prossimo gioco" CTA only for them.
    [Fact]
    public async Task Handle_ViewerIsOrganizer_IsViewerOrganizerTrue()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        result.IsViewerOrganizer.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ViewerIsInvitedNonOrganizer_IsViewerOrganizerFalse()
    {
        var invitedUserId = Guid.NewGuid();
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([invitedUserId]); // creates an RSVP → invited participant
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, invitedUserId), TestContext.Current.CancellationToken);

        result.IsViewerOrganizer.Should().BeFalse();
    }

    // WS1 DEC-9: the read model exposes the un-started planned games (with titles) so the
    // organizer CTA knows which game to start next.
    [Fact]
    public async Task Handle_ExposesUnstartedPlannedLineupWithTitles()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        _db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity { Id = g1, Title = "Brass" });
        _db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity { Id = g2, Title = "Catan" });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [g1, g2]);
        evt.Publish([]);
        evt.AddSession(Guid.NewGuid(), g1, "Brass"); // g1 started; g2 remains un-started
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        result.PlannedLineup.Should().ContainSingle();
        result.PlannedLineup[0].GameId.Should().Be(g2);
        result.PlannedLineup[0].GameTitle.Should().Be("Catan");
    }

    [Fact]
    public async Task Handle_NoSessions_ReturnsEmptyList()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata vuota", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        result.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata privata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        // A random authenticated user who is neither the organizer nor invited.
        var act = () => _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var act = () => _handler.Handle(
            new GetGameNightLiveQuery(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // #2634 C4: resolve GameNightSession.WinnerId (a tracking Participant.Id) to a display name,
    // scoped by (SessionId, WinnerId) so a foreign id fails closed to null.
    [Fact]
    public async Task Handle_ResolvesWinnerNameScopedBySession_AndNullsForeignIds()
    {
        var organizerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = winnerId, SessionId = sessionId, DisplayName = "Alice", UserId = Guid.NewGuid(), JoinOrder = 1
        });
        // A participant with the SAME id but a DIFFERENT session must NOT resolve (scoping).
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), DisplayName = "Bob", UserId = Guid.NewGuid(), JoinOrder = 1
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        evt.AddSession(sessionId, evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.CompleteCurrentSession(winnerId); // Completed with winner
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.Sessions[0].WinnerId.Should().Be(winnerId);
        result.Sessions[0].WinnerName.Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_ForeignWinnerId_ResolvesToNullName()
    {
        var organizerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        evt.AddSession(sessionId, evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.CompleteCurrentSession(Guid.NewGuid()); // winner id with NO matching participant row
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.Sessions[0].WinnerName.Should().BeNull();
    }

    // #2634 C4: the in-progress session's participants feed the winner picker off this guarded read.
    [Fact]
    public async Task Handle_ExposesInProgressSessionRoster()
    {
        var organizerId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = p1, SessionId = liveSessionId, DisplayName = "Host", UserId = organizerId, IsOwner = true, JoinOrder = 1
        });
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = p2, SessionId = liveSessionId, DisplayName = "Guest Gina", UserId = null, JoinOrder = 2
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        evt.AddSession(liveSessionId, evt.GameIds[0], "Catan");
        evt.StartCurrentSession(); // InProgress
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.CurrentSessionRoster.Should().HaveCount(2);
        result.CurrentSessionRoster.Should().Contain(m => m.ParticipantId == p1 && m.DisplayName == "Host");
        result.CurrentSessionRoster.Should().Contain(m => m.ParticipantId == p2 && m.DisplayName == "Guest Gina");
    }

    [Fact]
    public async Task Handle_NoLiveSession_RosterIsEmpty()
    {
        var organizerId = Guid.NewGuid();
        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]); // no session started
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.CurrentSessionRoster.Should().BeEmpty();
    }

    // #3188 Slice 1 (deploy-first forward-compat guard): a night whose session links are ALL Pending
    // — with no InProgress sibling — is a valid *draft* night. The live read must return 200 (project
    // the sessions, empty roster, night status passed through) instead of erroring. This pins the
    // tolerance as a baseline so later slices (3/6, which flip the direct-create path to be born
    // Pending) cannot silently regress it.
    [Fact]
    public async Task Handle_AllPendingLinks_ReturnsDraftNightWithoutError()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata bozza", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        evt.AddSession(s1, evt.GameIds[0], "Brass: Birmingham"); // Pending
        evt.AddSession(s2, evt.GameIds[1], "Spirit Island"); // Pending — none started, no InProgress sibling
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var act = () => _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, evt.OrganizerId), TestContext.Current.CancellationToken);

        var dto = (await act.Should().NotThrowAsync()).Subject;
        dto.Status.Should().Be(GameNightStatus.Published); // night status passed through unchanged
        dto.Sessions.Should().HaveCount(2);
        dto.Sessions.Should().OnlyContain(s => s.Status == GameNightSessionStatus.Pending);
        dto.CurrentSessionRoster.Should().BeEmpty();
    }

    // #3188 Slice 1: with a mixed night (some Pending + exactly one InProgress) the winner-picker
    // roster must be isolated to the single live session — a Pending session's participants never
    // leak in.
    [Fact]
    public async Task Handle_MixedPendingAndInProgress_RosterIsolatedToLiveSession()
    {
        var organizerId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var pendingSessionId = Guid.NewGuid();
        var liveP1 = Guid.NewGuid();
        var liveP2 = Guid.NewGuid();
        var pendingP = Guid.NewGuid();

        // Participants split across the live and the still-Pending session; only the live roster surfaces.
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = liveP1, SessionId = liveSessionId, DisplayName = "Host", UserId = organizerId, IsOwner = true, JoinOrder = 1
        });
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = liveP2, SessionId = liveSessionId, DisplayName = "Guest Gina", UserId = null, JoinOrder = 2
        });
        _db.SessionTrackingParticipants.Add(new Api.Infrastructure.Entities.SessionTracking.ParticipantEntity
        {
            Id = pendingP, SessionId = pendingSessionId, DisplayName = "Not Yet", UserId = null, JoinOrder = 1
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        evt.AddSession(liveSessionId, evt.GameIds[0], "Catan"); // PlayOrder 1
        evt.AddSession(pendingSessionId, evt.GameIds[1], "Azul"); // PlayOrder 2 — stays Pending
        evt.StartCurrentSession(); // lowest PlayOrder (liveSessionId) → InProgress
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.CurrentSessionRoster.Should().HaveCount(2);
        result.CurrentSessionRoster.Should().OnlyContain(m => m.ParticipantId == liveP1 || m.ParticipantId == liveP2);
        result.CurrentSessionRoster.Should().NotContain(m => m.ParticipantId == pendingP);
        // sanity: the InProgress + Pending mix is faithfully reflected in the projection.
        result.Sessions.Should().Contain(s => s.SessionId == liveSessionId && s.Status == GameNightSessionStatus.InProgress);
        result.Sessions.Should().Contain(s => s.SessionId == pendingSessionId && s.Status == GameNightSessionStatus.Pending);
    }
}

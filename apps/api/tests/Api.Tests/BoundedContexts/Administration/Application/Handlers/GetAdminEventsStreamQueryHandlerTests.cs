using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure.EventBroadcasting;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="GetAdminEventsStreamQueryHandler"/>.
///
/// F4.1 issue #1718 — verifies:
/// <list type="bullet">
///   <item><see cref="Handle_YieldsBackfillThenLiveStream"/> — subscribes eagerly, backfills when
///         LastEventId is set, yields live events with dedup.</item>
///   <item><see cref="Handle_NoLastEventId_YieldsOnlyLiveStream"/> — skips backfill entirely when
///         no cursor is provided.</item>
///   <item><see cref="Handle_DedupsSeedIdFromLiveStream"/> — backfill IDs are not re-emitted by the
///         live stream.</item>
///   <item><see cref="Constructor_ThrowsOnNullBroadcaster"/> / <see cref="Constructor_ThrowsOnNullMediator"/>
///         — guard clauses.</item>
/// </list>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class GetAdminEventsStreamQueryHandlerTests
{
    // ─────────────────────────────────────────────────────────────────────
    // Constructor guard clauses
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnNullBroadcaster()
    {
        var act = () => new GetAdminEventsStreamQueryHandler(null!, Mock.Of<IMediator>());
        act.Should().Throw<ArgumentNullException>().WithParameterName("broadcaster");
    }

    [Fact]
    public void Constructor_ThrowsOnNullMediator()
    {
        var act = () => new GetAdminEventsStreamQueryHandler(Mock.Of<IEventBroadcaster>(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediator");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Handle_YieldsBackfillThenLiveStream
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_YieldsBackfillThenLiveStream()
    {
        // Arrange
        // Events in DB (DESC order from GetAdminEventsQuery):
        //   newest (id3), middle (id2), cursor (id1) ← TakeWhile stops here
        var id1 = Guid.NewGuid(); // last-seen cursor
        var id2 = Guid.NewGuid(); // newer than cursor — should be backfilled
        var id3 = Guid.NewGuid(); // newest — should be backfilled
        var idLive = Guid.NewGuid(); // comes in via live channel after backfill

        var dbEvents = new List<DomainEventDto>
        {
            MakeDto(id3, DateTime.UtcNow.AddSeconds(-1)),  // index 0 — newest
            MakeDto(id2, DateTime.UtcNow.AddSeconds(-2)),  // index 1
            MakeDto(id1, DateTime.UtcNow.AddSeconds(-3)),  // index 2 — cursor (TakeWhile stops)
        };

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(
                It.Is<GetAdminEventsQuery>(q => q.Limit == 200),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAdminEventsResult(dbEvents));

        var liveEvent = MakeDto(idLive, DateTime.UtcNow);

        var broadcasterMock = new Mock<IEventBroadcaster>();
        broadcasterMock
            .Setup(b => b.Subscribe(It.IsAny<EventBroadcastFilter>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(liveEvent));

        var handler = new GetAdminEventsStreamQueryHandler(
            broadcasterMock.Object,
            mediatorMock.Object);

        var query = new GetAdminEventsStreamQuery(LastEventId: id1);

        // Act
        var results = new List<DomainEventDto>();
        await foreach (var evt in handler.Handle(query, CancellationToken.None))
        {
            results.Add(evt);
        }

        // Assert — backfill: id2, id3 (oldest-first after Reverse()); then live: idLive
        results.Should().HaveCount(3);
        results[0].Id.Should().Be(id2, "oldest backfill event emitted first after Reverse()");
        results[1].Id.Should().Be(id3, "newest backfill event emitted second");
        results[2].Id.Should().Be(idLive, "live event emitted after backfill");

        // Verify broadcaster was called BEFORE mediator (eager subscribe)
        var callOrder = new List<string>();
        var orderedBroadcaster = new Mock<IEventBroadcaster>(MockBehavior.Strict);
        orderedBroadcaster
            .Setup(b => b.Subscribe(It.IsAny<EventBroadcastFilter>(), It.IsAny<CancellationToken>()))
            .Callback<EventBroadcastFilter, CancellationToken>((_, _) => callOrder.Add("subscribe"))
            .Returns(CreateAsyncEnumerable());

        var orderedMediator = new Mock<IMediator>();
        orderedMediator
            .Setup(m => m.Send(It.IsAny<GetAdminEventsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<GetAdminEventsResult>, CancellationToken>((_, _) => callOrder.Add("send"))
            .ReturnsAsync(new GetAdminEventsResult(dbEvents));

        var handlerOrdered = new GetAdminEventsStreamQueryHandler(
            orderedBroadcaster.Object, orderedMediator.Object);
        await foreach (var _ in handlerOrdered.Handle(query, CancellationToken.None)) { }

        callOrder[0].Should().Be("subscribe", "broadcaster.Subscribe must be called BEFORE mediator.Send");
        callOrder[1].Should().Be("send");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Handle_NoLastEventId_YieldsOnlyLiveStream
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoLastEventId_YieldsOnlyLiveStream()
    {
        // Arrange
        var idLive = Guid.NewGuid();
        var liveEvent = MakeDto(idLive, DateTime.UtcNow);

        var mediatorMock = new Mock<IMediator>(MockBehavior.Strict); // must NOT be called
        var broadcasterMock = new Mock<IEventBroadcaster>();
        broadcasterMock
            .Setup(b => b.Subscribe(It.IsAny<EventBroadcastFilter>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(liveEvent));

        var handler = new GetAdminEventsStreamQueryHandler(
            broadcasterMock.Object, mediatorMock.Object);

        var query = new GetAdminEventsStreamQuery(); // LastEventId = null

        // Act
        var results = new List<DomainEventDto>();
        await foreach (var evt in handler.Handle(query, CancellationToken.None))
        {
            results.Add(evt);
        }

        // Assert — mediator must NOT have been called (no backfill without cursor)
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(idLive);
        mediatorMock.VerifyNoOtherCalls();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Handle_DedupsSeedIdFromLiveStream
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DedupsSeedIdFromLiveStream()
    {
        // Arrange — backfill returns id2 (newer than cursor id1);
        // live stream replays the same id2 (arrived during backfill window) + a fresh idLive.
        // id2 must appear exactly once in the output.
        var id1 = Guid.NewGuid(); // cursor
        var id2 = Guid.NewGuid(); // backfilled AND replayed by live stream
        var idLive = Guid.NewGuid(); // only from live stream

        var dbEvents = new List<DomainEventDto>
        {
            MakeDto(id2, DateTime.UtcNow.AddSeconds(-1)), // newer than cursor
            MakeDto(id1, DateTime.UtcNow.AddSeconds(-2)), // cursor — TakeWhile stops
        };

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAdminEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAdminEventsResult(dbEvents));

        // Live stream re-delivers id2 (duplicate) and then idLive (fresh)
        var broadcasterMock = new Mock<IEventBroadcaster>();
        broadcasterMock
            .Setup(b => b.Subscribe(It.IsAny<EventBroadcastFilter>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(
                MakeDto(id2, DateTime.UtcNow.AddSeconds(-1)), // duplicate — must be skipped
                MakeDto(idLive, DateTime.UtcNow)));            // fresh — must be emitted

        var handler = new GetAdminEventsStreamQueryHandler(
            broadcasterMock.Object, mediatorMock.Object);

        var query = new GetAdminEventsStreamQuery(LastEventId: id1);

        // Act
        var results = new List<DomainEventDto>();
        await foreach (var evt in handler.Handle(query, CancellationToken.None))
        {
            results.Add(evt);
        }

        // Assert
        results.Should().HaveCount(2, "id2 must appear only once; idLive must appear once");
        results[0].Id.Should().Be(id2, "backfill event emitted first");
        results[1].Id.Should().Be(idLive, "live-only event emitted after dedup");
        results.Should().NotContain(e => e.Id == id2 && results.IndexOf(e) > 0,
            "id2 must not be emitted a second time by the live stream");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Handle_FilterPassedToBroadcaster
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FilterPassedToBroadcaster()
    {
        // Arrange
        var expectedEventTypes = new[] { "agent.created" };
        var expectedAggregateTypes = new[] { "Agent" };
        var expectedUserId = Guid.NewGuid();
        var expectedAggregateId = Guid.NewGuid();

        EventBroadcastFilter? capturedFilter = null;
        var broadcasterMock = new Mock<IEventBroadcaster>();
        broadcasterMock
            .Setup(b => b.Subscribe(It.IsAny<EventBroadcastFilter>(), It.IsAny<CancellationToken>()))
            .Callback<EventBroadcastFilter, CancellationToken>((f, _) => capturedFilter = f)
            .Returns(CreateAsyncEnumerable());

        var mediatorMock = new Mock<IMediator>();
        var handler = new GetAdminEventsStreamQueryHandler(
            broadcasterMock.Object, mediatorMock.Object);

        var query = new GetAdminEventsStreamQuery(
            EventTypes: expectedEventTypes,
            AggregateTypes: expectedAggregateTypes,
            UserId: expectedUserId,
            AggregateId: expectedAggregateId);

        // Act
        await foreach (var _ in handler.Handle(query, CancellationToken.None)) { }

        // Assert
        capturedFilter.Should().NotBeNull();
        capturedFilter!.EventTypes.Should().BeEquivalentTo(expectedEventTypes);
        capturedFilter.AggregateTypes.Should().BeEquivalentTo(expectedAggregateTypes);
        capturedFilter.UserId.Should().Be(expectedUserId);
        capturedFilter.AggregateId.Should().Be(expectedAggregateId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static DomainEventDto MakeDto(Guid id, DateTime loggedAt) =>
        new(
            Id: id,
            EventId: Guid.NewGuid(),
            EventType: "agent.created",
            AggregateType: "Agent",
            AggregateId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            PayloadJson: "{}",
            PayloadVersion: 1,
            OccurredAt: loggedAt,
            LoggedAt: loggedAt);

    private static async IAsyncEnumerable<DomainEventDto> CreateAsyncEnumerable(
        params DomainEventDto[] events)
    {
        foreach (var evt in events)
        {
            yield return evt;
        }

        await Task.CompletedTask;
    }
}

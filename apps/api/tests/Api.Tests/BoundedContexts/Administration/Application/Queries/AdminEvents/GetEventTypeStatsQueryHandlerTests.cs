using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Unit tests for <see cref="GetEventTypeStatsQueryHandler"/> — Task 1.2 of
/// F4.1 issue #1718 (SP5 Admin Console — A8 Monitor + LiveEventLog).
///
/// Uses EF Core InMemory provider. The handler applies GROUP BY + COUNT + MAX
/// LINQ that translates identically in-memory and on Postgres for this shape.
/// Postgres-specific behaviors are out of scope; covered by integration tests in Task 1.5.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class GetEventTypeStatsQueryHandlerTests
{
    // Deterministic reference point — all LoggedAt values are relative to this.
    private static readonly DateTime _now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static MeepleAiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"event-type-stats-{Guid.NewGuid()}")
            .Options;

        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static DomainEventLogEntity MakeEvent(
        string eventType = "agent.created",
        DateTime? loggedAt = null)
    {
        var loggedAtValue = loggedAt ?? _now;
        return new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = eventType,
            AggregateType = "Agent",
            AggregateId = Guid.NewGuid(),
            UserId = null,
            PayloadJson = "{}",
            PayloadVersion = 1,
            OccurredAt = loggedAtValue,
            LoggedAt = loggedAtValue,
        };
    }

    private static GetEventTypeStatsQueryHandler CreateHandler(MeepleAiDbContext db)
        => new(db, new FakeTimeProvider(new DateTimeOffset(_now)));

    // -------------------------------------------------------------------------
    // Test 1: GROUP BY EventType — verifies count per type
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_GroupsByEventType_LastDay()
    {
        // Arrange
        await using var db = CreateDb();

        // Seed: 2 agent.created + 3 kb.doc.indexed + 1 session.created — all within 24h
        db.DomainEventLogs.AddRange(
            MakeEvent("agent.created", _now.AddMinutes(-10)),
            MakeEvent("agent.created", _now.AddMinutes(-20)),
            MakeEvent("kb.doc.indexed", _now.AddMinutes(-5)),
            MakeEvent("kb.doc.indexed", _now.AddMinutes(-15)),
            MakeEvent("kb.doc.indexed", _now.AddMinutes(-25)),
            MakeEvent("session.created", _now.AddMinutes(-30)));

        // One event OUTSIDE the 24h window — must not be counted
        db.DomainEventLogs.Add(MakeEvent("agent.created", _now.AddHours(-25)));

        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetEventTypeStatsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — each known EventTypeRegistry alias must appear in result
        result.Types.Should().NotBeNull();

        // The three active event types within the 24h window must be grouped correctly
        var agentStat = result.Types.Single(t => t.EventType == "agent.created");
        agentStat.Count.Should().Be(2);  // the 3rd one is outside 24h

        var kbStat = result.Types.Single(t => t.EventType == "kb.doc.indexed");
        kbStat.Count.Should().Be(3);

        var sessionStat = result.Types.Single(t => t.EventType == "session.created");
        sessionStat.Count.Should().Be(1);

        // Result is ordered alphabetically by EventType
        var activeTypes = result.Types.Where(t => t.Count > 0).Select(t => t.EventType).ToList();
        activeTypes.Should().BeInAscendingOrder();
    }

    // -------------------------------------------------------------------------
    // Test 2: Count = COUNT(*) and LastSeenAt = MAX(LoggedAt) per group
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReturnsCountAndLastSeenAt()
    {
        // Arrange
        await using var db = CreateDb();

        var t1 = _now.AddMinutes(-30);
        var t2 = _now.AddMinutes(-10); // most recent for agent.created
        var t3 = _now.AddMinutes(-5);  // most recent for kb.doc.indexed

        db.DomainEventLogs.AddRange(
            MakeEvent("agent.created", t1),
            MakeEvent("agent.created", t2),
            MakeEvent("kb.doc.indexed", t3));

        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetEventTypeStatsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — Count and LastSeenAt reflect aggregate per group
        var agentStat = result.Types.Single(t => t.EventType == "agent.created");
        agentStat.Count.Should().Be(2);
        agentStat.LastSeenAt.Should().Be(t2);  // MAX(LoggedAt) for agent.created

        var kbStat = result.Types.Single(t => t.EventType == "kb.doc.indexed");
        kbStat.Count.Should().Be(1);
        kbStat.LastSeenAt.Should().Be(t3);  // MAX(LoggedAt) for kb.doc.indexed
    }

    // -------------------------------------------------------------------------
    // Test 3: Empty DB — all Known types returned with Count=0, LastSeenAt=null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_EmptyDb_ReturnsKnownTypesWithZeroCount()
    {
        // Arrange
        await using var db = CreateDb();
        // No events seeded at all

        var handler = CreateHandler(db);
        var query = new GetEventTypeStatsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — all EventTypeRegistry.AliasByType.Values must appear in result
        var knownAliases = EventTypeRegistry.AliasByType.Values.ToHashSet();

        result.Types.Should().NotBeNull();
        result.Types.Select(t => t.EventType).Should().BeEquivalentTo(knownAliases);

        // Every type has Count=0 and LastSeenAt=null
        foreach (var stat in result.Types)
        {
            stat.Count.Should().Be(0, because: $"{stat.EventType} has no events in DB");
            stat.LastSeenAt.Should().BeNull(because: $"{stat.EventType} has no events in DB");
        }

        // Result is ordered alphabetically by EventType
        result.Types.Select(t => t.EventType).Should().BeInAscendingOrder();
    }
}

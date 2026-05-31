using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure;
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
/// Unit tests for <see cref="GetAdminEventsQueryHandler"/> — Task 1.1 of
/// F4.1 issue #1718 (SP5 Admin Console — A8 Monitor + LiveEventLog).
///
/// Uses EF Core InMemory provider. The handler applies pure LINQ predicates
/// that translate identically in-memory and on Postgres (cursor + retention +
/// optional filters + ORDER BY + Take). Postgres-only behaviors (index scan
/// performance, ILIKE) are out of scope for this unit suite; covered by the
/// integration tests in Task 1.5.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class GetAdminEventsQueryHandlerTests
{
    // Deterministic reference point — tests set LoggedAt relative to this
    private static readonly DateTime _now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static MeepleAiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin-events-{Guid.NewGuid()}")
            .Options;

        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    /// <summary>
    /// Builds a minimal valid <see cref="DomainEventLogEntity"/> seeded with
    /// deterministic values. Callers override only the fields relevant to the
    /// test case.
    /// </summary>
    private static DomainEventLogEntity MakeEvent(
        string eventType = "agent.created",
        string? aggregateType = "Agent",
        Guid? aggregateId = null,
        Guid? userId = null,
        DateTime? loggedAt = null,
        DateTime? occurredAt = null)
    {
        var loggedAtValue = loggedAt ?? _now;
        return new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId ?? Guid.NewGuid(),
            UserId = userId,
            PayloadJson = "{}",
            PayloadVersion = 1,
            OccurredAt = occurredAt ?? loggedAtValue,
            LoggedAt = loggedAtValue,
        };
    }

    /// <summary>
    /// Creates a handler with a <see cref="FakeTimeProvider"/> anchored at <see cref="_now"/>.
    /// All retention-window calculations in the handler are deterministic regardless of wall-clock.
    /// </summary>
    private static GetAdminEventsQueryHandler CreateHandler(MeepleAiDbContext db)
        => new(db, new FakeTimeProvider(new DateTimeOffset(_now)));

    private static GetAdminEventsQueryHandler CreateHandler(MeepleAiDbContext db, TimeProvider timeProvider)
        => new(db, timeProvider);

    // -------------------------------------------------------------------------
    // Test 1: ORDER BY LoggedAt DESC
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReturnsEvents_OrderedByLoggedAtDesc()
    {
        // Arrange
        await using var db = CreateDb();

        var older = MakeEvent(loggedAt: _now.AddMinutes(-30));
        var newest = MakeEvent(loggedAt: _now);
        var middle = MakeEvent(loggedAt: _now.AddMinutes(-10));

        db.DomainEventLogs.AddRange(older, newest, middle);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — descending by LoggedAt
        result.Events.Should().HaveCount(3);
        result.Events[0].LoggedAt.Should().Be(_now);
        result.Events[1].LoggedAt.Should().Be(_now.AddMinutes(-10));
        result.Events[2].LoggedAt.Should().Be(_now.AddMinutes(-30));
    }

    // -------------------------------------------------------------------------
    // Test 2: 90-day retention — events older than 90 days excluded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_AppliesRetention_90Days()
    {
        // Arrange
        await using var db = CreateDb();

        // Handler uses FakeTimeProvider anchored at _now (2026-05-31T12:00:00Z).
        // retentionCutoff = _now.AddDays(-90) = 2026-03-02T12:00:00Z.
        // Boundary: LoggedAt >= retentionCutoff → _now-89d and _now-90d are included;
        //           _now-91d is strictly before cutoff → excluded.
        // This test is deterministic forever regardless of wall-clock. (Issue #1718 fix.)
        var within = MakeEvent(loggedAt: _now.AddDays(-89));    // inside retention window
        var exact = MakeEvent(loggedAt: _now.AddDays(-90));     // boundary — == cutoff → included
        var tooOld = MakeEvent(loggedAt: _now.AddDays(-91));    // strictly before cutoff → excluded

        db.DomainEventLogs.AddRange(within, exact, tooOld);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Retention is the only filter here (no EventTypes, no UserId, etc.)
        var query = new GetAdminEventsQuery(Limit: 1000);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — tooOld (91d before _now) excluded; within + exact included
        result.Events.Should().HaveCount(2);
        result.Events.Select(e => e.LoggedAt).Should().NotContain(_now.AddDays(-91));
        result.Events.Select(e => e.LoggedAt).Should().Contain(_now.AddDays(-89));
        result.Events.Select(e => e.LoggedAt).Should().Contain(_now.AddDays(-90));
    }

    // -------------------------------------------------------------------------
    // Test 3: Since cursor — LoggedAt < Since
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WithSinceCursor_FiltersOlderThanSince()
    {
        // Arrange
        await using var db = CreateDb();

        var cursor = _now.AddMinutes(-20);
        var beforeCursor = MakeEvent(loggedAt: _now.AddMinutes(-30));  // included (< cursor)
        var atCursor = MakeEvent(loggedAt: cursor);                     // excluded (not < cursor)
        var afterCursor = MakeEvent(loggedAt: _now.AddMinutes(-10));   // excluded (> cursor)

        db.DomainEventLogs.AddRange(beforeCursor, atCursor, afterCursor);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery(Since: cursor);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — only the event strictly before the cursor is returned
        result.Events.Should().ContainSingle();
        result.Events[0].LoggedAt.Should().Be(_now.AddMinutes(-30));
    }

    // -------------------------------------------------------------------------
    // Test 4: EventTypes filter — IN list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WithEventTypesFilter_AppliesIn()
    {
        // Arrange
        await using var db = CreateDb();

        var agentCreated = MakeEvent(eventType: "agent.created");
        var kbIndexed = MakeEvent(eventType: "kb.doc.indexed");
        var sessionCreated = MakeEvent(eventType: "session.created");

        db.DomainEventLogs.AddRange(agentCreated, kbIndexed, sessionCreated);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery(
            EventTypes: new[] { "agent.created", "kb.doc.indexed" });

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — only the two requested event types returned
        result.Events.Should().HaveCount(2);
        result.Events.Select(e => e.EventType)
            .Should().BeEquivalentTo(new[] { "agent.created", "kb.doc.indexed" });
    }

    // -------------------------------------------------------------------------
    // Test 5: AggregateTypes filter — IN list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WithAggregateTypesFilter_AppliesIn()
    {
        // Arrange
        await using var db = CreateDb();

        var agentEvent = MakeEvent(aggregateType: "Agent");
        var pdfEvent = MakeEvent(aggregateType: "PdfDocument");
        var sessionEvent = MakeEvent(aggregateType: "Session");

        db.DomainEventLogs.AddRange(agentEvent, pdfEvent, sessionEvent);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery(
            AggregateTypes: new[] { "Agent", "PdfDocument" });

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Events.Should().HaveCount(2);
        result.Events.Select(e => e.AggregateType)
            .Should().BeEquivalentTo(new[] { "Agent", "PdfDocument" });
    }

    // -------------------------------------------------------------------------
    // Test 6: UserId equality filter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WithUserIdFilter_AppliesEquality()
    {
        // Arrange
        await using var db = CreateDb();

        var targetUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var targetEvent = MakeEvent(userId: targetUserId);
        var otherEvent = MakeEvent(userId: otherUserId);
        var nullUserEvent = MakeEvent(userId: null);

        db.DomainEventLogs.AddRange(targetEvent, otherEvent, nullUserEvent);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery(UserId: targetUserId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — only the target user's event returned
        result.Events.Should().ContainSingle();
        result.Events[0].UserId.Should().Be(targetUserId);
    }

    // -------------------------------------------------------------------------
    // Test 7: AggregateId equality filter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WithAggregateIdFilter_AppliesEquality()
    {
        // Arrange
        await using var db = CreateDb();

        var targetAggId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var otherAggId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        var targetEvent = MakeEvent(aggregateId: targetAggId);
        var otherEvent = MakeEvent(aggregateId: otherAggId);

        db.DomainEventLogs.AddRange(targetEvent, otherEvent);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery(AggregateId: targetAggId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Events.Should().ContainSingle();
        result.Events[0].AggregateId.Should().Be(targetAggId);
    }

    // -------------------------------------------------------------------------
    // Test 8: Limit — default 100, clamped to 1..1000
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_AppliesLimit()
    {
        // Arrange
        await using var db = CreateDb();

        // Seed 150 events, all within retention
        for (var i = 0; i < 150; i++)
        {
            db.DomainEventLogs.Add(MakeEvent(loggedAt: _now.AddSeconds(-i)));
        }
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Default limit = 100 (record default)
        var defaultResult = await handler.Handle(new GetAdminEventsQuery(), CancellationToken.None);
        defaultResult.Events.Should().HaveCount(100);

        // Explicit limit = 50
        var limitedResult = await handler.Handle(
            new GetAdminEventsQuery(Limit: 50), CancellationToken.None);
        limitedResult.Events.Should().HaveCount(50);

        // Limit = 0 → clamped to 1
        var zeroResult = await handler.Handle(
            new GetAdminEventsQuery(Limit: 0), CancellationToken.None);
        zeroResult.Events.Should().HaveCount(1);

        // Limit = 9999 → clamped to 1000 (only 150 rows exist → returns 150)
        var overResult = await handler.Handle(
            new GetAdminEventsQuery(Limit: 9999), CancellationToken.None);
        overResult.Events.Should().HaveCount(150);
    }

    // -------------------------------------------------------------------------
    // Test 9: Empty result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyList()
    {
        // Arrange
        await using var db = CreateDb();
        // No events seeded

        var handler = CreateHandler(db);
        var query = new GetAdminEventsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().BeEmpty();
    }
}

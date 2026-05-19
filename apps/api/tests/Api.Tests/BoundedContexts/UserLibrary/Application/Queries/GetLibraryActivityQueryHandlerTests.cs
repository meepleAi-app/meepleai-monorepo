using System.Text.Json;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetLibraryActivityQueryHandler"/> issue #661 PR-B.
/// Covers AC-5 (full integration of log + row sources), AC-6 (retention filter),
/// AC-12 (merge/order correctness).
///
/// Uses EF Core InMemory; the production EF query is straightforward LINQ that
/// translates to the same in-memory result, so the assertions hold across
/// providers. Postgres-only behaviors (the GIN index, the LoggedAt date math)
/// are covered by the existing <c>LibraryActivityEndpointTests</c> integration
/// suite.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "661")]
public sealed class GetLibraryActivityQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"661-pr-b-{Guid.NewGuid()}")
            .Options;

        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    // -----------------------------------------------------------------------
    // AC-5 — removed event from the log surfaces in the feed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_RemovedEventInLog_AppearsAsRemovedKind()
    {
        await using var db = CreateInMemoryContext();

        var gameId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { gameId, userId = UserId });

        db.DomainEventLogs.Add(new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "library.entry.removed",
            UserId = UserId,
            AggregateId = Guid.NewGuid(),
            PayloadJson = payload,
            OccurredAt = DateTime.UtcNow.AddMinutes(-5),
            LoggedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var handler = new GetLibraryActivityQueryHandler(db);
        var result = await handler.Handle(new GetLibraryActivityQuery(UserId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Type.Should().Be("removed");
        result[0].GameId.Should().Be(gameId);
    }

    // -----------------------------------------------------------------------
    // AC-5b — session-recorded event from the log surfaces in the feed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_SessionRecordedEventInLog_AppearsAsSessionRecordedKind()
    {
        await using var db = CreateInMemoryContext();

        var gameId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { gameId, userId = UserId, sessionId = Guid.NewGuid() });

        db.DomainEventLogs.Add(new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "library.session.recorded",
            UserId = UserId,
            AggregateId = Guid.NewGuid(),
            PayloadJson = payload,
            OccurredAt = DateTime.UtcNow.AddMinutes(-2),
            LoggedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var handler = new GetLibraryActivityQueryHandler(db);
        var result = await handler.Handle(new GetLibraryActivityQuery(UserId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Type.Should().Be("session-recorded");
        result[0].GameId.Should().Be(gameId);
    }

    // -----------------------------------------------------------------------
    // AC-6 — events older than the retention cutoff are filtered out
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_LogRowOlderThanRetention_IsFilteredOut()
    {
        await using var db = CreateInMemoryContext();

        var payload = JsonSerializer.Serialize(new { gameId = Guid.NewGuid(), userId = UserId });

        // 91-day-old event (just over default retention of 90 days).
        db.DomainEventLogs.Add(new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "library.entry.removed",
            UserId = UserId,
            AggregateId = Guid.NewGuid(),
            PayloadJson = payload,
            OccurredAt = DateTime.UtcNow.AddDays(-91),
            LoggedAt = DateTime.UtcNow.AddDays(-91),
        });
        await db.SaveChangesAsync();

        var handler = new GetLibraryActivityQueryHandler(db);
        var result = await handler.Handle(new GetLibraryActivityQuery(UserId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // AC-5 — events for OTHER users are not surfaced
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_LogRowForOtherUser_IsNotSurfaced()
    {
        await using var db = CreateInMemoryContext();

        var payload = JsonSerializer.Serialize(new { gameId = Guid.NewGuid(), userId = OtherUserId });

        db.DomainEventLogs.Add(new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "library.entry.removed",
            UserId = OtherUserId,
            AggregateId = Guid.NewGuid(),
            PayloadJson = payload,
            OccurredAt = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var handler = new GetLibraryActivityQueryHandler(db);
        var result = await handler.Handle(new GetLibraryActivityQuery(UserId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // AC-12 — merge ordering: legacy + log events interleave by timestamp
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_LegacyAndLogEvents_InterleaveByTimestampDesc()
    {
        await using var db = CreateInMemoryContext();
        var now = DateTime.UtcNow;

        var gameId = Guid.NewGuid();
        // Legacy library entry — produces an "added" event at T-3d via row projection.
        // We use PrivateGameId to avoid seeding a full SharedGame aggregate (which has
        // mandatory fields specific to its persistence model — out of scope here).
        var libraryEntryId = Guid.NewGuid();
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = libraryEntryId,
            UserId = UserId,
            PrivateGameId = gameId,
            CurrentState = (int)GameStateType.Owned,
            AddedAt = now.AddDays(-3),
            StateChangedAt = null,
        });

        // Log event at T-1d (more recent than "added").
        db.DomainEventLogs.Add(new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "library.session.recorded",
            UserId = UserId,
            AggregateId = libraryEntryId,
            PayloadJson = JsonSerializer.Serialize(new { gameId, userId = UserId }),
            OccurredAt = now.AddDays(-1),
            LoggedAt = now.AddDays(-1),
        });

        await db.SaveChangesAsync();

        var handler = new GetLibraryActivityQueryHandler(db);
        var result = await handler.Handle(new GetLibraryActivityQuery(UserId), CancellationToken.None);

        // Both events present, ordered DESC by timestamp (session-recorded first).
        result.Should().HaveCount(2);
        result[0].Type.Should().Be("session-recorded");
        result[1].Type.Should().Be("added");
    }
}

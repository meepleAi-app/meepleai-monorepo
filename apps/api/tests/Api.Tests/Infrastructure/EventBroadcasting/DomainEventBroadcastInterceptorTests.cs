using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.Infrastructure.EventBroadcasting;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Infrastructure.EventBroadcasting;

/// <summary>
/// Integration and unit tests for <see cref="DomainEventBroadcastInterceptor"/> — Task 1.4
/// of F4.1 issue #1718 (SP5 Admin Console — A8 Monitor + LiveEventLog).
///
/// Three cases verify:
///   1. <see cref="SaveChanges_PublishesEachNewDomainEventLog"/> — each added
///      <see cref="DomainEventLogEntity"/> triggers one <see cref="IEventBroadcaster.Publish"/>
///      call after a successful commit (Testcontainers Postgres).
///   2. <see cref="SaveChanges_RollbackOnError_NoPublish"/> — if SaveChanges throws due to a
///      DB constraint violation, Publish is never invoked (Testcontainers Postgres).
///   3. <see cref="Publishes_AfterCommit_NotBefore"/> — Publish is called AFTER SaveChanges
///      returns (post-commit ordering verified via EF Core InMemory + flag capture).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
[Collection("Integration-GroupD")]
public sealed class DomainEventBroadcastInterceptorTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    public DomainEventBroadcastInterceptorTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_domeventinterceptor{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Migrate the isolated DB so domain_event_logs table + indexes exist.
        await using var ctx = BuildDbContextWithInterceptor(new Mock<IEventBroadcaster>().Object);
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a real Postgres-backed <see cref="MeepleAiDbContext"/> with
    /// <see cref="DomainEventBroadcastInterceptor"/> attached.
    /// The <see cref="IDomainEventCollector"/> mock returns an empty event list so the
    /// DbContext's own domain-event dispatch logic is a no-op for these tests.
    /// </summary>
    private MeepleAiDbContext BuildDbContextWithInterceptor(IEventBroadcaster broadcaster)
    {
        var interceptor = new DomainEventBroadcastInterceptor(broadcaster);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString, o => o.UseVector())
            .AddInterceptors(interceptor)
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        // PeekEvents() used by MeepleAiDbContext.SaveChangesAsync Step 1.
        mockEventCollector
            .Setup(e => e.PeekEvents())
            .Returns(Array.Empty<IDomainEvent>());
        // Clear() called by MeepleAiDbContext.SaveChangesAsync Step 4 (drain after success).
        mockEventCollector.Setup(e => e.Clear());

        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    /// <summary>
    /// Creates a minimal <see cref="DomainEventLogEntity"/> for testing.
    /// All required string columns are non-null.
    /// </summary>
    private static DomainEventLogEntity MakeEventLog(
        Guid? eventId = null,
        string eventType = "agent.created")
        => new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId ?? Guid.NewGuid(),
            EventType = eventType,
            AggregateType = "Agent",
            AggregateId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PayloadJson = "{}",
            PayloadVersion = 1,
            OccurredAt = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow,
        };

    // -------------------------------------------------------------------------
    // Test 1: SaveChanges_PublishesEachNewDomainEventLog
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that adding two <see cref="DomainEventLogEntity"/> rows in a single
    /// SaveChanges call results in exactly two <see cref="IEventBroadcaster.Publish"/>
    /// invocations, with DTOs that match the entities, AFTER the commit succeeds.
    /// </summary>
    [Fact]
    public async Task SaveChanges_PublishesEachNewDomainEventLog()
    {
        // Arrange
        var broadcasterMock = new Mock<IEventBroadcaster>();
        var publishedDtos = new List<DomainEventDto>();
        broadcasterMock
            .Setup(b => b.Publish(It.IsAny<DomainEventDto>()))
            .Callback<DomainEventDto>(dto => publishedDtos.Add(dto));

        await using var ctx = BuildDbContextWithInterceptor(broadcasterMock.Object);

        var entity1 = MakeEventLog(eventType: "agent.created");
        var entity2 = MakeEventLog(eventType: "session.created");

        // Act
        ctx.DomainEventLogs.Add(entity1);
        ctx.DomainEventLogs.Add(entity2);
        await ctx.SaveChangesAsync();

        // Assert — Publish called once per added entity
        broadcasterMock.Verify(b => b.Publish(It.IsAny<DomainEventDto>()), Times.Exactly(2));

        publishedDtos.Should().HaveCount(2);
        publishedDtos.Should().ContainSingle(d =>
            d.EventType == "agent.created" && d.Id == entity1.Id && d.EventId == entity1.EventId);
        publishedDtos.Should().ContainSingle(d =>
            d.EventType == "session.created" && d.Id == entity2.Id && d.EventId == entity2.EventId);
    }

    // -------------------------------------------------------------------------
    // Test 2: SaveChanges_RollbackOnError_NoPublish
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies transactional safety: if SaveChanges throws (duplicate EventId UNIQUE
    /// constraint violation), <see cref="IEventBroadcaster.Publish"/> is never invoked.
    /// </summary>
    [Fact]
    public async Task SaveChanges_RollbackOnError_NoPublish()
    {
        // Arrange — seed one entity with a known EventId
        var sharedEventId = Guid.NewGuid();

        await using var seedCtx = BuildDbContextWithInterceptor(new Mock<IEventBroadcaster>().Object);
        seedCtx.DomainEventLogs.Add(MakeEventLog(eventId: sharedEventId));
        await seedCtx.SaveChangesAsync();

        // Act — second SaveChanges with the same EventId → UNIQUE constraint violation
        var broadcasterMock = new Mock<IEventBroadcaster>(MockBehavior.Strict);
        // Strict mock: any unexpected call to Publish will throw, making the test fail loudly.

        await using var conflictCtx = BuildDbContextWithInterceptor(broadcasterMock.Object);
        conflictCtx.DomainEventLogs.Add(MakeEventLog(eventId: sharedEventId)); // duplicate EventId

        var act = async () => await conflictCtx.SaveChangesAsync();

        // Assert — SaveChanges throws AND Publish is never called
        await act.Should().ThrowAsync<DbUpdateException>();
        broadcasterMock.Verify(b => b.Publish(It.IsAny<DomainEventDto>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 3 (Publishes_AfterCommit_NotBefore) has been moved to
    // DomainEventBroadcastInterceptorOrderingTests so it does not trigger the
    // Integration-GroupD Testcontainers fixture on the Backend Fast CI job
    // (no Docker). Issue #1873 review follow-up.
    // -------------------------------------------------------------------------
}

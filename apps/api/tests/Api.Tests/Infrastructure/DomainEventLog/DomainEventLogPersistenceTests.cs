using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.DomainEventLog;

/// <summary>
/// Unit tests for the atomic-save flow in <see cref="MeepleAiDbContext.SaveChangesAsync"/>.
/// Issue #661 spec §3.2 hardened — covers AC-2, AC-2b, AC-11, AC-13.
///
/// Uses EF Core InMemory provider because the assertions target the in-process
/// flow (collector clear timing, log row materialization, MediatR call order).
/// The UNIQUE constraint check (AC-11) requires a relational provider; that
/// AC is covered by integration tests against Testcontainers Postgres in PR-B.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "661")]
public sealed class DomainEventLogPersistenceTests
{
    // -----------------------------------------------------------------------
    // Test fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// A stub event that IS registered for log persistence (added via
    /// <see cref="RegisterStubAlias"/>).
    /// </summary>
    private sealed record StubLoggedEvent(Guid UserId, Guid AggregateId) : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// A stub event that is NOT registered — should flow through MediatR but
    /// NOT appear in the log table.
    /// </summary>
    private sealed record StubUnregisteredEvent(Guid UserId) : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Register a temporary alias for StubLoggedEvent before each test. The
    /// registry's AliasByType is internal-mutable for this purpose — see
    /// EventTypeRegistry.cs.
    /// </summary>
    private static IDisposable RegisterStubAlias()
    {
        // The registry's backing field is private + mutable; tests reach into
        // it via reflection to add ad-hoc test aliases without polluting the
        // production registration.
        var field = typeof(EventTypeRegistry).GetField(
            "_aliasByType",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_aliasByType field not found");

        var original = (Dictionary<Type, string>)field.GetValue(null)!;
        var augmented = new Dictionary<Type, string>(original)
        {
            [typeof(StubLoggedEvent)] = "test.stub.logged",
        };
        field.SetValue(null, augmented);

        return new Restore(() => field.SetValue(null, original));
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _action;
        public Restore(Action action) => _action = action;
        public void Dispose() => _action();
    }

    private static MeepleAiDbContext CreateInMemoryContext(
        IDomainEventCollector collector,
        IMediator mediator,
        ILogger<MeepleAiDbContext>? logger = null)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"661-{Guid.NewGuid()}")
            .Options;

        return new MeepleAiDbContext(options, mediator, collector, dataProtectionProvider: null, logger);
    }

    // -----------------------------------------------------------------------
    // AC-2 — Registered event is persisted to domain_event_logs ATOMICALLY
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_RegisteredEvent_PersistsLogRowAndDispatchesViaMediatR()
    {
        using var _aliasScope = RegisterStubAlias();

        var collector = new DomainEventCollector();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var dbContext = CreateInMemoryContext(collector, mediator.Object);

        // Arrange — manually add a domain event to the collector (simulates a
        // repository call before SaveChangesAsync).
        var ev = new StubLoggedEvent(Guid.NewGuid(), Guid.NewGuid());
        var aggregate = new StubAggregate(ev);
        collector.CollectEventsFrom(aggregate);

        // Act
        await dbContext.SaveChangesAsync();

        // Assert — log row exists with the registered alias.
        var logRow = dbContext.DomainEventLogs.SingleOrDefault();
        logRow.Should().NotBeNull();
        logRow!.EventType.Should().Be("test.stub.logged");
        logRow.EventId.Should().Be(ev.EventId);
        logRow.UserId.Should().Be(ev.UserId);
        logRow.AggregateId.Should().Be(ev.AggregateId);

        // Mediator was published exactly once for the event.
        mediator.Verify(
            m => m.Publish(It.Is<IDomainEvent>(x => x.EventId == ev.EventId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // AC-2 — Unregistered event is NOT persisted but IS still dispatched
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_UnregisteredEvent_NotLoggedButStillDispatched()
    {
        var collector = new DomainEventCollector();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var dbContext = CreateInMemoryContext(collector, mediator.Object);

        var ev = new StubUnregisteredEvent(Guid.NewGuid());
        collector.CollectEventsFrom(new StubAggregate(ev));

        await dbContext.SaveChangesAsync();

        // No log row.
        dbContext.DomainEventLogs.Should().BeEmpty();
        // But mediator IS published — in-memory consumers are unchanged.
        mediator.Verify(
            m => m.Publish(It.Is<IDomainEvent>(x => x.EventId == ev.EventId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // AC-2b — Collector is drained ONLY after a successful save
    // -----------------------------------------------------------------------

    [Fact]
    public void Collector_PeekEventsIsNonDestructive_AndClearIsExplicit()
    {
        var collector = new DomainEventCollector();
        var ev1 = new StubUnregisteredEvent(Guid.NewGuid());
        var ev2 = new StubUnregisteredEvent(Guid.NewGuid());
        collector.CollectEventsFrom(new StubAggregate(ev1));
        collector.CollectEventsFrom(new StubAggregate(ev2));

        // Peek multiple times — same set each time.
        collector.PeekEvents().Should().HaveCount(2);
        collector.PeekEvents().Should().HaveCount(2);

        // Clear is explicit.
        collector.Clear();
        collector.PeekEvents().Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // AC-13 — Mediator.Publish failure logs ERROR without throwing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_MediatorPublishFails_LogsErrorAndDoesNotThrow()
    {
        using var _aliasScope = RegisterStubAlias();

        var collector = new DomainEventCollector();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler exploded"));

        var logger = new Mock<ILogger<MeepleAiDbContext>>();
        using var dbContext = CreateInMemoryContext(collector, mediator.Object, logger.Object);

        var ev = new StubLoggedEvent(Guid.NewGuid(), Guid.NewGuid());
        collector.CollectEventsFrom(new StubAggregate(ev));

        // Act — must NOT throw despite the mediator failure.
        var saveAct = async () => await dbContext.SaveChangesAsync();
        await saveAct.Should().NotThrowAsync();

        // Log row was still persisted (the durable record).
        dbContext.DomainEventLogs.Should().ContainSingle();

        // ERROR-level log line was emitted with the failing event info.
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MediatR.Publish failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private sealed class StubAggregate : IAggregateRoot
    {
        private readonly List<IDomainEvent> _events;
        public StubAggregate(params IDomainEvent[] events) => _events = events.ToList();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _events.AsReadOnly();
        public void ClearDomainEvents() => _events.Clear();
    }
}

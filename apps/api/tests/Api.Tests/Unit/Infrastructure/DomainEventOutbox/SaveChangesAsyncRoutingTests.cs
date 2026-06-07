using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// Issue #1535 T3 — verifies the routing matrix wired into <c>MeepleAiDbContext.SaveChangesAsync</c>.
///
/// <para>For each <see cref="DomainEventDispatchMode"/>, an event raised during a save MUST
/// (a) appear or not appear in the <c>domain_event_outbox</c> table, and (b) trigger or not
/// trigger <c>MediatR.Publish</c>. The matrix:</para>
///
/// <list type="bullet">
///   <item><see cref="DomainEventDispatchMode.Hybrid"/>: write outbox row AND publish inline.</item>
///   <item><see cref="DomainEventDispatchMode.OutboxOnly"/>: write outbox row only — NO inline publish.</item>
///   <item><see cref="DomainEventDispatchMode.InlineOnly"/>: publish inline only — NO outbox row.</item>
/// </list>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class SaveChangesAsyncRoutingTests
{
    private sealed record FakeEvent(Guid Marker) : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; } = new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    private static (MeepleAiDbContext Db, Mock<IMediator> Mediator) CreateContextWithMode(
        DomainEventDispatchMode mode,
        IReadOnlyList<IDomainEvent> events)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"outbox-routing-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        var mediator = new Mock<IMediator>();
        // MeepleAiDbContext calls the generic IMediator.Publish<T>(T, ct) where T is inferred
        // from the static type of the variable. Setup must therefore match the IDomainEvent
        // variant — Moq does not collapse the two overloads automatically.
        mediator.Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var collector = new Mock<IDomainEventCollector>();
        collector.Setup(c => c.PeekEvents()).Returns(events);
        collector.Setup(c => c.Clear());

        var outboxOptions = Options.Create(new DomainEventOutboxOptions { Mode = mode });

        var db = new MeepleAiDbContext(
            options,
            mediator.Object,
            collector.Object,
            dataProtectionProvider: null,
            logger: null,
            domainEventOutboxOptions: outboxOptions);

        return (db, mediator);
    }

    [Fact]
    public async Task Hybrid_mode_writes_outbox_AND_publishes_inline()
    {
        // Arrange
        var ev = new FakeEvent(Guid.NewGuid());
        var (db, mediator) = CreateContextWithMode(DomainEventDispatchMode.Hybrid, new[] { ev });
        await using var _ = db;

        // Act
        await db.SaveChangesAsync();

        // Assert — outbox row present
        var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1, "Hybrid mode persists the outbox row alongside the inline publish");
        rows[0].Id.Should().Be(ev.EventId);
        rows[0].Status.Should().Be(DomainEventOutboxStatus.Pending);

        // Assert — inline publish fired
        mediator.Verify(
            m => m.Publish(It.Is<IDomainEvent>(o => ReferenceEquals(o, ev)), It.IsAny<CancellationToken>()),
            Times.Once,
            "Hybrid mode also publishes inline so consumers continue to fire same-tx (Phase A behaviour)");
    }

    [Fact]
    public async Task OutboxOnly_mode_writes_outbox_but_does_NOT_publish_inline()
    {
        var ev = new FakeEvent(Guid.NewGuid());
        var (db, mediator) = CreateContextWithMode(DomainEventDispatchMode.OutboxOnly, new[] { ev });
        await using var _ = db;

        await db.SaveChangesAsync();

        var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1, "OutboxOnly mode persists the outbox row — processor will dispatch post-commit");

        mediator.Verify(
            m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "OutboxOnly mode MUST NOT inline-publish: that's the bug #1535 was opened to fix");
    }

    [Fact]
    public async Task InlineOnly_mode_publishes_inline_but_does_NOT_write_outbox()
    {
        var ev = new FakeEvent(Guid.NewGuid());
        var (db, mediator) = CreateContextWithMode(DomainEventDispatchMode.InlineOnly, new[] { ev });
        await using var _ = db;

        await db.SaveChangesAsync();

        var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync();
        rows.Should().BeEmpty("InlineOnly is the legacy rollback path — no outbox row written");

        mediator.Verify(
            m => m.Publish(It.Is<IDomainEvent>(o => ReferenceEquals(o, ev)), It.IsAny<CancellationToken>()),
            Times.Once,
            "InlineOnly mode preserves the legacy inline publish");
    }

    [Fact]
    public async Task Default_mode_when_options_null_is_Hybrid()
    {
        // Older test fixtures that construct the DbContext without IOptions get the safe default.
        var ev = new FakeEvent(Guid.NewGuid());
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"outbox-default-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var collector = new Mock<IDomainEventCollector>();
        collector.Setup(c => c.PeekEvents()).Returns(new[] { ev });

        // Construct without the options parameter — backward-compat call site.
        await using var db = new MeepleAiDbContext(options, mediator.Object, collector.Object);

        await db.SaveChangesAsync();

        var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1, "default mode is Hybrid — outbox row written");
        mediator.Verify(m => m.Publish(It.Is<IDomainEvent>(o => ReferenceEquals(o, ev)), It.IsAny<CancellationToken>()),
            Times.Once, "default mode is Hybrid — inline publish still fires");
    }
}

using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Services;

/// <summary>
/// #4841 — what survives of ONE delivery of a domain event, when the same event reaches the dispatcher
/// again. The in-app row carries the event's <c>SourceEventId</c> and is the dedup marker; the channel
/// deliveries (email, Slack) are separate rows. They used to be staged only AFTER the marker was committed,
/// and no caller saves afterwards: in Hybrid mode (inline dispatch, then the outbox delivers the same event)
/// the marker was durable and the deliveries were not. That was harmless while the redelivered event got a
/// fresh <c>EventId</c> (the dispatcher then created everything again, duplicating the in-app row); with a
/// stable <c>EventId</c> the second delivery is skipped and the deliveries would be lost for good.
///
/// <para>Real repositories on the InMemory provider; the collaborators that only read are doubles.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationDispatcherDurabilityTests
{
    private static NotificationDispatcher BuildDispatcher(MeepleAiDbContext db)
    {
        var collector = Mock.Of<IDomainEventCollector>();
        return new NotificationDispatcher(
            new NotificationRepository(db, collector, Mock.Of<IUserNotificationBroadcaster>()),
            new NotificationQueueRepository(db, collector, NullLogger<NotificationQueueRepository>.Instance),
            Mock.Of<INotificationPreferencesRepository>(),
            Mock.Of<ISlackConnectionRepository>(),
            Options.Create(new SlackNotificationConfiguration()),
            TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);
    }

    private static NotificationMessage Message(Guid userId, Guid sourceEventId) => new()
    {
        Type = NotificationType.DocumentReady,
        RecipientUserId = userId,
        Payload = new PdfProcessingPayload(Guid.NewGuid(), "rulebook.pdf", "Ready"),
        SourceEventId = sourceEventId,
    };

    private static (int InApp, int Deliveries) Durable(string databaseName)
    {
        using var reader = TestDbContextFactory.CreateInMemoryDbContext(databaseName);
        return (reader.Set<NotificationEntity>().Count(), reader.Set<NotificationQueueEntity>().Count());
    }

    [Fact]
    public async Task Redelivery_after_a_dispatch_that_nobody_saved_afterwards_still_finds_the_deliveries()
    {
        // Hybrid: the first delivery is inline and nothing saves after it; the second comes from the outbox.
        var database = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var inline = TestDbContextFactory.CreateInMemoryDbContext(database))
        {
            await BuildDispatcher(inline).DispatchAsync(Message(userId, eventId));
        }

        using (var fromTheOutbox = TestDbContextFactory.CreateInMemoryDbContext(database))
        {
            await BuildDispatcher(fromTheOutbox).DispatchAsync(Message(userId, eventId));
        }

        var (inApp, deliveries) = Durable(database);
        inApp.Should().Be(1, "the second delivery is the same event: one in-app row");
        deliveries.Should().Be(1, "the email of that one notification must be durable, not left staged on a context nobody saves");
    }

    [Fact]
    public async Task Redelivery_after_the_processor_final_save_creates_neither_a_second_row_nor_a_second_delivery()
    {
        // OutboxOnly: the processor saves once more at the end of the batch, then a later attempt redelivers.
        var database = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var attempt = TestDbContextFactory.CreateInMemoryDbContext(database))
        {
            await BuildDispatcher(attempt).DispatchAsync(Message(userId, eventId));
            await attempt.SaveChangesAsync();
        }

        using (var retry = TestDbContextFactory.CreateInMemoryDbContext(database))
        {
            await BuildDispatcher(retry).DispatchAsync(Message(userId, eventId));
            await retry.SaveChangesAsync();
        }

        var (inApp, deliveries) = Durable(database);
        inApp.Should().Be(1);
        deliveries.Should().Be(1);
    }

    [Fact]
    public async Task Losing_the_insert_race_leaves_no_delivery_staged_for_a_later_save()
    {
        // The in-app insert is refused by the unique index (a concurrent caller won). The deliveries are
        // staged together with it, so they must be dropped with it: a later save of this context must not
        // create emails for a notification this caller never created.
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;
        using var db = new RefusesTheFirstSaveDbContext(options, Mock.Of<IDomainEventCollector>());

        await BuildDispatcher(db).DispatchAsync(Message(Guid.NewGuid(), Guid.NewGuid()));

        db.SavesAttempted.Should().Be(1, "the in-app insert was attempted and refused");
        db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Should().BeEmpty(
            "nothing of this caller may stay staged: the next SaveChangesAsync on this context would persist it");
    }

    private sealed class RefusesTheFirstSaveDbContext(
        DbContextOptions<MeepleAiDbContext> options,
        IDomainEventCollector collector)
        : MeepleAiDbContext(options, new Mock<IMediator>().Object, collector)
    {
        public int SavesAttempted { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SavesAttempted++;
            if (SavesAttempted == 1)
            {
                throw new DbUpdateException(
                    "duplicate key value violates unique constraint",
                    new PostgresException("duplicate key value", "ERROR", "ERROR", "23505"));
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

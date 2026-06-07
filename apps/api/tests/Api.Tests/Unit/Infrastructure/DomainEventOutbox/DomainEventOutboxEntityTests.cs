using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// Issue #1535 T1.B — entity state machine for the outbox row. Pure POCO; no DB.
/// Verifies that the lifecycle invariants are enforced inside the aggregate so the
/// processor's <c>MarkSent</c>/<c>MarkRetry</c>/<c>MarkFailed</c> calls cannot
/// produce inconsistent state by accident.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class DomainEventOutboxEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    private sealed record FakeDomainEvent(Guid AggregateId) : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; } = new DateTime(2026, 6, 7, 11, 59, 0, DateTimeKind.Utc);
    }

    private static DomainEventOutboxEntity CreatePending() =>
        DomainEventOutboxEntity.Enqueue(
            ev: new FakeDomainEvent(Guid.NewGuid()),
            eventType: "fake.event",
            payloadJson: "{\"id\":\"abc\"}",
            payloadVersion: 1,
            correlationId: "test-corr",
            now: Now);

    [Fact]
    public void Enqueue_creates_pending_row_with_event_id_as_pk()
    {
        // Arrange
        var ev = new FakeDomainEvent(Guid.NewGuid());

        // Act
        var row = DomainEventOutboxEntity.Enqueue(
            ev, "fake.event", "{}", 1, "corr-1", Now);

        // Assert
        row.Id.Should().Be(ev.EventId, "the outbox row PK is the originating event id (idempotency contract)");
        row.EventType.Should().Be("fake.event");
        row.PayloadJson.Should().Be("{}");
        row.PayloadVersion.Should().Be(1);
        row.Status.Should().Be(DomainEventOutboxStatus.Pending);
        row.Attempts.Should().Be(0);
        row.LastError.Should().BeNull();
        row.OccurredAt.Should().Be(ev.OccurredAt);
        row.EnqueuedAt.Should().Be(Now);
        row.DispatchedAt.Should().BeNull();
        row.NextAttemptAt.Should().BeNull();
        row.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public void MarkSent_transitions_from_Pending_only()
    {
        var row = CreatePending();

        row.MarkSent(Now.AddSeconds(2));

        row.Status.Should().Be(DomainEventOutboxStatus.Sent);
        row.DispatchedAt.Should().Be(Now.AddSeconds(2));
        row.LastError.Should().BeNull();
        row.NextAttemptAt.Should().BeNull();

        // Cannot re-Sent
        var act = () => row.MarkSent(Now.AddSeconds(3));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRetry_increments_attempts_and_schedules_next()
    {
        var row = CreatePending();
        var next = Now.AddSeconds(2);

        row.MarkRetry("transient connection error", next, Now);

        row.Attempts.Should().Be(1);
        row.LastError.Should().Be("transient connection error");
        row.NextAttemptAt.Should().Be(next);
        row.Status.Should().Be(DomainEventOutboxStatus.Pending,
            "MarkRetry keeps the row Pending so the processor picks it up after next_attempt_at");
        row.DispatchedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_terminal_no_further_state_change()
    {
        var row = CreatePending();

        row.MarkFailed("deterministic error", Now);

        row.Status.Should().Be(DomainEventOutboxStatus.Failed);
        row.Attempts.Should().Be(1);
        row.LastError.Should().Be("deterministic error");
        row.NextAttemptAt.Should().BeNull();

        // Cannot leave Failed
        var sentAct = () => row.MarkSent(Now);
        sentAct.Should().Throw<InvalidOperationException>();
        var retryAct = () => row.MarkRetry("x", Now.AddSeconds(1), Now);
        retryAct.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LastError_truncates_to_2048_chars()
    {
        var row = CreatePending();
        var huge = new string('x', 5000);

        row.MarkRetry(huge, Now.AddSeconds(1), Now);

        row.LastError.Should().NotBeNull();
        row.LastError!.Length.Should().Be(2048);
    }
}

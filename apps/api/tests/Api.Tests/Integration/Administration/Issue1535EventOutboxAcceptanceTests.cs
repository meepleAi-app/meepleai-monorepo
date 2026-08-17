using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Issue #1535 Phase 4 Task 7 — acceptance gate for post-commit domain-event dispatch.
///
/// <para>The 5 Given/When/Then scenarios from the kickoff
/// (<c>audits/2026-06-06-issue-1535-event-outbox-kickoff.md</c> § Acceptance scenarios)
/// were critiqued by the spec-panel before implementation. The panel's adopted design:</para>
///
/// <list type="number">
///   <item><b>Scenario 1 — Happy path E2E</b>: real CQRS pipeline. A registered event
///         (<see cref="PdfMetadataChangedEvent"/>) emitted via <see cref="IDomainEventCollector"/>
///         + <see cref="MeepleAiDbContext.SaveChangesAsync"/> in <c>OutboxOnly</c> mode is dispatched
///         by <see cref="DomainEventOutboxProcessor"/>. <b>Delta vs T4</b>: T4 seeds the row
///         directly; this seeds via the real collector path so it covers the DbContext routing
///         step too.</item>
///
///   <item><b>Scenario 2 — Rollback safety</b>: closes the original #1535 bug. An aggregate
///         mutation runs inside an explicit transaction with the outbox row INSERTed by
///         SaveChangesAsync; a sabotage throw triggers <c>tx.RollbackAsync()</c>. The outbox row
///         must NEVER be visible outside the transaction and the processor must NEVER dispatch.</item>
///
///   <item><b>Scenario 3 — Retry sequence</b>: complements T5's terminal-state test by asserting
///         the FULL temporal sequence (Attempts 1 → 2 → 3, Status Pending → Pending → Failed) over
///         3 consecutive <see cref="DomainEventOutboxProcessor.RunOnceAsync"/> calls, advancing a
///         <see cref="FakeTimeProvider"/> past each row's scheduled NextAttemptAt.</item>
///
///   <item><b>Scenario 4 — At-least-once delivery</b>: renamed from the kickoff's mislabeled
///         "crash recovery" (impossible to simulate in-process). A row's first dispatch attempt
///         throws (MarkRetry); the second succeeds (MarkSent). <see cref="IMediator.Publish"/> is
///         observed to fire TWICE for the same EventId — documenting the at-least-once contract
///         every consumer must honour.</item>
///
///   <item><b>Scenario 5 — Concurrent dispatch</b>: <b>SKIPPED</b> with the
///         <c>1535-Concurrency-Hardening</c> trait. The scenario tests an acknowledged limitation
///         (no <c>SELECT … FOR UPDATE SKIP LOCKED</c>), not a contract — see the test method's
///         XML doc-comment for rationale and the follow-up issue link.</item>
/// </list>
///
/// <para>Test infrastructure mirrors <c>DomainEventOutboxProcessorIntegrationTests</c>:
/// Testcontainers Postgres (<c>Integration-GroupD</c>), <see cref="IMediator"/> mocked, real
/// <see cref="MeepleAiDbContext"/> + processor + collector wired via
/// <see cref="IntegrationServiceCollectionBuilder"/>.</para>
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1535")]
public sealed class Issue1535EventOutboxAcceptanceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private Mock<IMediator>? _mediatorMock;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public Issue1535EventOutboxAcceptanceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_outbox_acceptance_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Override IMediator — we want to OBSERVE Publish invocations, not run the real handler
        // graph. Default no-op stub so registered handlers (cache invalidation etc.) don't
        // run; per-scenario tests override with throw-on-publish setups when needed.
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.RemoveAll<IMediator>();
        services.AddSingleton<IMediator>(mediatorMock.Object);
        _mediatorMock = mediatorMock;

        // Real resolver — scans the Api assembly. PdfMetadataChangedEvent is registered in
        // EventTypeRegistry (alias "pdf.metadata.changed"), so it round-trips through the
        // resolver's alias-first lookup.
        services.AddSingleton<IDomainEventTypeResolver, DomainEventTypeResolver>();

        // OutboxOnly mode = the Phase B target state. Acceptance tests exercise the final
        // production routing, not the Hybrid dual-write rollout window.
        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions
            {
                Mode = DomainEventDispatchMode.OutboxOnly,
                PollIntervalSeconds = 5,
                BatchSize = 100,
                MaxAttempts = 10,
                InitialBackoffMs = 1000,
                MaxBackoffSeconds = 64.0,
            }));

        services.AddSingleton<IDomainEventOutboxHealthTracker, DomainEventOutboxHealthTracker>();
        services.AddSingleton<DomainEventOutboxProcessor>();

        _serviceProvider = services.BuildServiceProvider();

        using var bootstrapScope = _serviceProvider.CreateScope();
        var bootstrapDb = bootstrapScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await ApplyMigrationsAsync(bootstrapDb);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null) await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 1 — Happy path E2E (real CQRS pipeline)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Given an event raised via <see cref="IDomainEventCollector.Collect"/>
    /// When the next <see cref="MeepleAiDbContext.SaveChangesAsync"/> runs in OutboxOnly mode
    /// Then exactly ONE outbox row exists with Status=Pending, PayloadJson round-trips,
    ///      and <see cref="IMediator.Publish"/> has NOT been called yet (no in-tx dispatch).
    ///
    /// When <see cref="DomainEventOutboxProcessor.RunOnceAsync"/> drains the row
    /// Then <see cref="IMediator.Publish"/> is called exactly once
    ///      with an event whose EventId matches the source, Status transitions to Sent,
    ///      and DispatchedAt is populated.
    ///
    /// <para>Delta vs T4 happy path: T4 seeds the row via direct <c>DomainEventOutbox.Add()</c> —
    /// THIS scenario triggers the row through the real <see cref="MeepleAiDbContext"/> routing
    /// (Step 2b of SaveChangesAsync), proving the upstream emission path works end-to-end.</para>
    /// </summary>
    [Fact]
    public async Task Scenario1_HappyPath_EndToEnd_DispatchesViaRealPipeline()
    {
        // Arrange: a registered event with a deterministic shape that round-trips through
        // DomainEventJsonOptions. PdfMetadataChangedEvent uses an init-only record — covers
        // both the alias lookup (resolver) and the JsonConstructor path (deserializer).
        var sourceEvent = new PdfMetadataChangedEvent(
            AggregateId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            EditorRole: "Admin",
            Changes: new List<MetadataChange>
            {
                new("DocumentType", "Manual", "Reference"),
            },
            GameId: Guid.NewGuid());

        // Act phase 1: emit via the real collector → SaveChangesAsync routes to outbox.
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var collector = scope.ServiceProvider.GetRequiredService<IDomainEventCollector>();
            collector.Collect(sourceEvent);
            await db.SaveChangesAsync(TestCancellationToken);
        }

        // Assert phase 1: exactly one Pending row + NO inline dispatch (OutboxOnly mode).
        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync(TestCancellationToken);
            rows.Should().HaveCount(1);
            rows[0].Id.Should().Be(sourceEvent.EventId);
            rows[0].Status.Should().Be(DomainEventOutboxStatus.Pending);
            rows[0].EventType.Should().Be("pdf.metadata.changed",
                because: "EventTypeRegistry resolves PdfMetadataChangedEvent to its stable alias");
            rows[0].PayloadJson.Should().Contain(sourceEvent.AggregateId.ToString(),
                because: "the payload must round-trip the source event's fields");
            rows[0].DispatchedAt.Should().BeNull();
        }

        _mediatorMock!.Verify(
            m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "OutboxOnly mode dispatches POST-commit only — no in-SaveChanges Publish allowed");

        // Act phase 2: drain via the real processor.
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);

        // Assert phase 2: exactly one Sent row + exactly one Publish invocation with matching EventId.
        processed.Should().Be(1);

        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<IDomainEvent>(e => e.EventId == sourceEvent.EventId),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the processor must invoke Publish exactly once for the deserialised event");

        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.DomainEventOutbox.AsNoTracking().SingleAsync(TestCancellationToken);
            row.Status.Should().Be(DomainEventOutboxStatus.Sent);
            row.DispatchedAt.Should().NotBeNull();
            row.Attempts.Should().Be(0);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 2 — Rollback safety (closes the original #1535 bug)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Given an aggregate mutation that emits a domain event INSIDE an explicit transaction
    /// And the outbox row is INSERTed by SaveChangesAsync (visible inside the tx)
    /// When a sabotage <c>throw</c> triggers <c>tx.RollbackAsync()</c>
    /// Then no outbox row exists OUTSIDE the transaction for that EventId
    /// And <see cref="IMediator.Publish"/> is NEVER invoked for that event by the processor.
    ///
    /// <para>This is the scenario that closes the original #1535 bug: pre-fix, the inline
    /// MediatR.Publish inside SaveChangesAsync fired BEFORE the outer transaction committed —
    /// so a downstream rollback (audit enqueue failure, retry on transient error) left
    /// committed side-effects in Redis / email queue / SSE without a way to undo them.</para>
    ///
    /// <para>Post-fix (OutboxOnly mode): the row is INSERTed in the same tx as the aggregate;
    /// the processor only sees rows that were COMMITTED. Rollback ⇒ row never visible ⇒ zero
    /// dispatch ⇒ zero leaked side-effects.</para>
    ///
    /// <para>Implementation note: we simulate the failure with an explicit
    /// <c>BeginTransactionAsync</c> instead of the real <c>[AtomicAudit]</c> behaviour
    /// because the latter has heavy auth / userId / interceptor setup that adds no signal to
    /// this guarantee. The semantic — "tx rollback ⇒ row never visible to the processor" —
    /// is identical.</para>
    /// </summary>
    [Fact]
    public async Task Scenario2_RollbackSafety_RowNeverVisible_NoDispatch()
    {
        var sourceEvent = new PdfMetadataChangedEvent(
            AggregateId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            EditorRole: "Owner",
            Changes: new List<MetadataChange> { new("Language", "en", "it") },
            GameId: null);

        // Act: explicit transaction that commits the outbox INSERT and then rolls back.
        // The Postgres execution strategy (NpgsqlRetryingExecutionStrategy) bars naked
        // BeginTransactionAsync calls — they MUST run inside the strategy's ExecuteAsync
        // delegate so a transient failure can replay the whole unit. Within the delegate
        // we deliberately ROLL BACK and propagate a sabotage exception out to mimic the
        // original #1535 failure mode (a post-SaveChanges throw during the outer commit).
        var rollbackTriggered = false;
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var collector = scope.ServiceProvider.GetRequiredService<IDomainEventCollector>();
            var strategy = db.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database
                        .BeginTransactionAsync(TestCancellationToken)
                        .ConfigureAwait(false);

                    collector.Collect(sourceEvent);
                    await db.SaveChangesAsync(TestCancellationToken).ConfigureAwait(false);

                    // Sentinel — confirm the row IS visible INSIDE the tx so we know the
                    // rollback is the thing hiding it (not a wiring bug). Same connection
                    // + same tx, so this read sees uncommitted state.
                    var insideTx = await db.DomainEventOutbox
                        .AsNoTracking()
                        .Where(r => r.Id == sourceEvent.EventId)
                        .CountAsync(TestCancellationToken)
                        .ConfigureAwait(false);
                    insideTx.Should().Be(1,
                        because: "the row must be persisted by SaveChangesAsync before we attempt rollback");

                    await tx.RollbackAsync(TestCancellationToken).ConfigureAwait(false);
                    rollbackTriggered = true;
                    throw new InvalidOperationException("sabotage-rollback-trigger");
                });
            }
            catch (InvalidOperationException ex) when (ex.Message == "sabotage-rollback-trigger")
            {
                // Expected — propagated out of the strategy delegate. The execution strategy
                // does NOT retry on InvalidOperationException (only transient Npgsql codes).
            }
        }

        rollbackTriggered.Should().BeTrue();

        // Assert: a fresh scope (separate connection) sees NO row for that EventId.
        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var outsideTx = await db.DomainEventOutbox
                .AsNoTracking()
                .Where(r => r.Id == sourceEvent.EventId)
                .CountAsync(TestCancellationToken);
            outsideTx.Should().Be(0,
                because: "tx.RollbackAsync MUST hide the outbox INSERT from any other connection — " +
                "this is the guarantee that closes the #1535 race condition");
        }

        // Assert: the processor running against the now-empty outbox dispatches nothing.
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);
        processed.Should().Be(0);

        _mediatorMock!.Verify(
            m => m.Publish(
                It.Is<IDomainEvent>(e => e.EventId == sourceEvent.EventId),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "no side-effect may escape from a rolled-back transaction — this is the #1535 fix");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 3 — Retry sequence (1 → 2 → 3 over 3 consecutive runs)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Given an outbox row whose mediator handler throws a deterministic exception
    /// And MaxAttempts is configured to 3
    /// When the processor runs 3 times consecutively, advancing a <see cref="FakeTimeProvider"/>
    ///   past each scheduled <c>NextAttemptAt</c> between runs
    /// Then the row transitions through the full sequence:
    ///   Run 1: Status=Pending, Attempts=1, NextAttemptAt set (backoff)
    ///   Run 2: Status=Pending, Attempts=2, NextAttemptAt advanced
    ///   Run 3: Status=Failed,  Attempts=3, NextAttemptAt=null (terminal)
    /// And every transition carries the deterministic exception message in LastError.
    ///
    /// <para>Delta vs T5: T5's <c>RunOnceAsync_AfterMaxAttempts_MarksFailed_Terminal</c> seeds
    /// a row with Attempts=2 and runs the processor once to verify the FINAL transition. This
    /// scenario asserts the FULL TEMPORAL SEQUENCE — useful for proving the increment +
    /// NextAttemptAt scheduling logic stays coherent across multiple polls.</para>
    /// </summary>
    [Fact]
    public async Task Scenario3_RetryBudget_TemporalSequence_PendingPendingFailed()
    {
        const string deterministicMessage = "deterministic-acceptance";
        _mediatorMock!
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(deterministicMessage));

        var startTime = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startTime);

        // Seed: 1 Pending row, ready immediately.
        Guid rowId;
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var ev = new PdfMetadataChangedEvent(
                AggregateId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                EditorRole: "Admin",
                Changes: new List<MetadataChange> { new("DocumentType", "Manual", "Reference") },
                GameId: null);
            var row = DomainEventOutboxEntity.Enqueue(
                ev,
                eventType: "pdf.metadata.changed",
                payloadJson: System.Text.Json.JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: startTime);
            db.DomainEventOutbox.Add(row);
            await db.SaveChangesAsync(TestCancellationToken);
            rowId = row.Id;
        }

        // MaxAttempts=3 override: terminal transition lands on the 3rd run.
        var processor = CreateProcessorWithOptions(
            new DomainEventOutboxOptions { MaxAttempts = 3, InitialBackoffMs = 1000, MaxBackoffSeconds = 64 },
            timeProvider);

        // Run 1 → Attempts=1, Pending, NextAttemptAt ≈ now + 1s.
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);
        var snapshot1 = await ReadRowAsync(rowId);
        snapshot1.Status.Should().Be(DomainEventOutboxStatus.Pending);
        snapshot1.Attempts.Should().Be(1);
        snapshot1.LastError.Should().Be(deterministicMessage);
        snapshot1.NextAttemptAt.Should().NotBeNull();

        // Advance the clock past the scheduled retry, then Run 2 → Attempts=2.
        timeProvider.SetUtcNow(snapshot1.NextAttemptAt!.Value.AddSeconds(1));
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);
        var snapshot2 = await ReadRowAsync(rowId);
        snapshot2.Status.Should().Be(DomainEventOutboxStatus.Pending);
        snapshot2.Attempts.Should().Be(2);
        snapshot2.NextAttemptAt.Should().NotBeNull();

        // Advance again, then Run 3 → Attempts=3, terminal Failed.
        timeProvider.SetUtcNow(snapshot2.NextAttemptAt!.Value.AddSeconds(1));
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);
        var snapshot3 = await ReadRowAsync(rowId);
        snapshot3.Status.Should().Be(DomainEventOutboxStatus.Failed,
            because: "Attempts(2)+1 == MaxAttempts(3) ⇒ row terminates");
        snapshot3.Attempts.Should().Be(3);
        snapshot3.NextAttemptAt.Should().BeNull(
            because: "terminal Failed rows must not be re-scheduled");

        // Mediator was called once per run = 3 times total.
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 4 — At-least-once delivery (renamed from kickoff's "crash recovery")
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Given an outbox row in Pending
    /// When the processor's first <see cref="IMediator.Publish"/> attempt throws (transient)
    /// And the processor's second attempt succeeds
    /// Then <see cref="IMediator.Publish"/> has been invoked TWICE for the same EventId
    /// And the row ends in Sent.
    ///
    /// <para><b>Why renamed from "crash recovery"</b>: the kickoff G/W/T described "processor
    /// killed BETWEEN Publish and SaveChanges". That scenario is not faithfully reproducible
    /// in an in-process unit test — there is no way to "kill" a C# method mid-execution. The
    /// observable contract the system actually provides is <i>at-least-once delivery</i>: a
    /// failed dispatch (transient or otherwise) leaves the row Pending and the next poll will
    /// re-Publish. Real crashes manifest as the same observable behaviour — Pending rows on
    /// restart — so this test covers the recovery path that matters.</para>
    ///
    /// <para><b>Consumer contract</b>: this test is the executable witness for the rule
    /// stated in <c>audits/2026-06-06-issue-1535-consumer-idempotency-audit.md</c>: every
    /// <see cref="INotificationHandler{TNotification}"/> for an <see cref="IDomainEvent"/> MUST
    /// be idempotent because the dispatch can fire 1..N times for a single EventId.</para>
    /// </summary>
    [Fact]
    public async Task Scenario4_AtLeastOnceDelivery_PublishFiresTwice_RowEndsSent()
    {
        // Mediator: throw on the first Publish call, succeed on every subsequent call.
        var callCount = 0;
        _mediatorMock!
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns<INotification, CancellationToken>((_, _) =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 1)
                {
                    throw new InvalidOperationException("first-attempt-transient");
                }
                return Task.CompletedTask;
            });

        var startTime = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startTime);

        // Seed: 1 Pending row, ready immediately.
        Guid rowId;
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var ev = new PdfMetadataChangedEvent(
                AggregateId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                EditorRole: "Admin",
                Changes: new List<MetadataChange> { new("Title", "old", "new") },
                GameId: null);
            var row = DomainEventOutboxEntity.Enqueue(
                ev,
                eventType: "pdf.metadata.changed",
                payloadJson: System.Text.Json.JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: startTime);
            db.DomainEventOutbox.Add(row);
            await db.SaveChangesAsync(TestCancellationToken);
            rowId = row.Id;
        }

        var processor = CreateProcessorWithOptions(
            new DomainEventOutboxOptions { MaxAttempts = 10, InitialBackoffMs = 1000, MaxBackoffSeconds = 64 },
            timeProvider);

        // Run 1: Publish throws → MarkRetry. Row stays Pending. callCount=1.
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);
        var afterFirst = await ReadRowAsync(rowId);
        afterFirst.Status.Should().Be(DomainEventOutboxStatus.Pending);
        afterFirst.Attempts.Should().Be(1);
        callCount.Should().Be(1,
            because: "the first dispatch attempt fired Publish exactly once — that is observable");

        // Advance the clock past the backoff window so the row is ready again.
        timeProvider.SetUtcNow(afterFirst.NextAttemptAt!.Value.AddSeconds(1));

        // Run 2: Publish succeeds → MarkSent. callCount=2.
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);
        var afterSecond = await ReadRowAsync(rowId);
        afterSecond.Status.Should().Be(DomainEventOutboxStatus.Sent);
        afterSecond.DispatchedAt.Should().NotBeNull();

        callCount.Should().Be(2,
            because: "at-least-once delivery: the same EventId fired Publish twice " +
            "— consumers MUST be idempotent (see consumer-idempotency-audit doc)");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Scenario 5 — Concurrent dispatch (multi-instance) — SKIPPED with tracker
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <para><b>Status: SKIPPED.</b> Tracker: <c>1535-Concurrency-Hardening</c>.</para>
    ///
    /// <para>The original G/W/T (kickoff § Scenario 5) describes "2 processor instances running
    /// concurrently against the same DB; assert ≤ 200 Publish invocations for 100 rows". This
    /// is <b>not an acceptance criterion</b> — it asserts an <i>acknowledged limitation</i>, not
    /// a guarantee. The plan explicitly lists work-stealing via <c>SELECT … FOR UPDATE SKIP
    /// LOCKED</c> as a <b>non-goal</b> for the MVP (plan § Non-goals line 13). Writing a test
    /// that documents "the system may double-publish" provides no regression signal — any
    /// implementation that respects the at-least-once contract (Scenario 4) trivially passes.</para>
    ///
    /// <para>The right place for concurrency hardening is a <i>follow-up issue</i>, gated on
    /// observed duplicate-publish rate exceeding 5% in staging (per the kickoff's own
    /// criterion). When that threshold is breached, the follow-up should:</para>
    /// <list type="number">
    ///   <item>Add <c>SELECT … FOR UPDATE SKIP LOCKED</c> to the processor's pending-query.</item>
    ///   <item>Add this test (re-enabled) asserting exactly-once dispatch under concurrency.</item>
    ///   <item>Update the consumer-contract doc to soften the idempotency requirement.</item>
    /// </list>
    /// </summary>
    [Fact(Skip = "1535-Concurrency-Hardening — see XML doc for rationale.")]
    public Task Scenario5_ConcurrentDispatch_MultiInstance_BoundedDuplicates()
    {
        // Intentionally empty: the XML doc-comment IS the documentation. Re-enabling this
        // test means implementing FOR UPDATE SKIP LOCKED first.
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task ApplyMigrationsAsync(MeepleAiDbContext db)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
        }
    }

    /// <summary>
    /// Builds a <see cref="DomainEventOutboxProcessor"/> with per-test option overrides
    /// (MaxAttempts, backoff knobs) and a deterministic <see cref="TimeProvider"/>. The
    /// container-registered processor uses production defaults — Scenarios 3 and 4 need
    /// MaxAttempts=3 and a clock they control.
    /// </summary>
    private DomainEventOutboxProcessor CreateProcessorWithOptions(
        DomainEventOutboxOptions options,
        TimeProvider timeProvider)
    {
        var scopeFactory = _serviceProvider!.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider!.GetRequiredService<ILogger<DomainEventOutboxProcessor>>();
        var healthTracker = _serviceProvider!.GetRequiredService<IDomainEventOutboxHealthTracker>();
        return new DomainEventOutboxProcessor(
            scopeFactory,
            logger,
            Options.Create(options),
            healthTracker,
            timeProvider);
    }

    /// <summary>
    /// Reads a single outbox row by id via a fresh scope (no tracking) so the assertion sees
    /// the post-commit state, not the change-tracker view of the scope that mutated it.
    /// </summary>
    private async Task<DomainEventOutboxEntity> ReadRowAsync(Guid rowId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.DomainEventOutbox
            .AsNoTracking()
            .SingleAsync(r => r.Id == rowId, TestCancellationToken);
    }
}

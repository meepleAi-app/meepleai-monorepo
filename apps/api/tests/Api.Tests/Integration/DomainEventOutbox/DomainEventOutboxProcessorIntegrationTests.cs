using System.Diagnostics.Metrics;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.Observability;
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

namespace Api.Tests.Integration.DomainEventOutbox;

/// <summary>
/// Issue #1535 Phase 2 Task 4 — happy-path integration tests for
/// <see cref="DomainEventOutboxProcessor"/>.
///
/// <para>Drain contract under test (Plan T4 step 1):</para>
/// <list type="bullet">
///   <item>Test 1: 3 Pending rows → 3 dispatched + marked Sent.</item>
///   <item>Test 2: row with <c>NextAttemptAt</c> in the future → skipped (0 processed).</item>
///   <item>Test 3: FIFO ordering by <c>EnqueuedAt</c> respected when batch is undersized.</item>
///   <item>Test 4: empty queue returns 0 AND refreshes the health snapshot (0/0/0).</item>
/// </list>
///
/// <para>Mediator + resolver + health tracker are mocked so the assertions can be
/// tight (per-Publish counts, Sent transitions, health snapshot calls) without
/// depending on the real domain-event handler graph.</para>
///
/// <para>Postgres via Testcontainers is mandatory — the transactional drain semantics
/// (ExecutionStrategy + BeginTransaction + Commit) cannot be exercised faithfully on
/// InMemory.</para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Infrastructure")]
public sealed class DomainEventOutboxProcessorIntegrationTests : IAsyncLifetime
{
    private const string FakeEventAlias = "test.fake.event";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private Mock<IMediator>? _mediatorMock;
    private Mock<IDomainEventOutboxHealthTracker>? _healthMock;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DomainEventOutboxProcessorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_domain_event_outbox_processor_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Override IMediator — production wiring would dispatch the real handler graph
        // (cache invalidations, SSE broadcasts, etc.). We only care that the processor
        // CALLS Publish with the deserialised event, not that downstream handlers run.
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.RemoveAll<IMediator>();
        services.AddSingleton<IMediator>(mediatorMock.Object);
        _mediatorMock = mediatorMock;

        // Resolver mock: maps our test alias back to the FakeEvent CLR type. The real
        // resolver scans the Api assembly, so a test-local event type is invisible to it
        // — mocking is the cleanest way to keep the test self-contained.
        var resolverMock = new Mock<IDomainEventTypeResolver>();
        resolverMock.Setup(r => r.Resolve(FakeEventAlias)).Returns(typeof(FakeEvent));
        services.AddSingleton<IDomainEventTypeResolver>(resolverMock.Object);

        // Health tracker mock so we can Verify the snapshot call shape per test.
        var healthMock = new Mock<IDomainEventOutboxHealthTracker>();
        services.AddSingleton<IDomainEventOutboxHealthTracker>(healthMock.Object);
        _healthMock = healthMock;

        // Options + processor itself. Production uses AddHostedService; here we resolve
        // the processor directly and drive RunOnceAsync explicitly for determinism.
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
    // Test 1 — happy path: 3 Pending rows dispatched and marked Sent
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_DispatchesPendingRows_AndMarksSent()
    {
        // Arrange: 3 Pending rows ready immediately (NextAttemptAt = null).
        var now = DateTimeOffset.UtcNow;
        var seeded = new List<DomainEventOutboxEntity>();
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            for (var i = 0; i < 3; i++)
            {
                var ev = new FakeEvent(Marker: i);
                var row = DomainEventOutboxEntity.Enqueue(
                    ev,
                    FakeEventAlias,
                    JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                    payloadVersion: 1,
                    correlationId: null,
                    now: now.AddSeconds(i));
                db.DomainEventOutbox.Add(row);
                seeded.Add(row);
            }
            await db.SaveChangesAsync(TestCancellationToken);
        }

        // Act: drain a single batch sized to fit all 3 rows.
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 100, cancellationToken: TestCancellationToken);

        // Assert: per-batch count + state transitions + downstream dispatch.
        processed.Should().Be(3,
            because: "RunOnceAsync returns the number of rows it considered in this batch");

        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var rows = await db.DomainEventOutbox.AsNoTracking()
                .OrderBy(r => r.EnqueuedAt)
                .ToListAsync(TestCancellationToken);
            rows.Should().HaveCount(3);
            rows.Should().AllSatisfy(r =>
            {
                r.Status.Should().Be(DomainEventOutboxStatus.Sent);
                r.DispatchedAt.Should().NotBeNull();
                r.Attempts.Should().Be(0);
            });
        }

        _mediatorMock!.Verify(
            m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3),
            "each dispatched row must invoke MediatR.Publish exactly once");

        _healthMock!.Verify(
            t => t.RecordSnapshot(It.IsAny<long>(), It.IsAny<double>(), It.IsAny<long>()),
            Times.AtLeastOnce,
            "the processor must refresh the health snapshot after the batch");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // #2923 — dispatched counter emits one series per event_type (cardinality parity)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_EmitsDispatchedCounter_PerEventType()
    {
        // Arrange: two Pending rows carrying DISTINCT event_type aliases mapped to the same CLR
        // fixture (the counter tags on row.EventType, not the CLR type). #2923: this proves the
        // processor emits meepleai.domain_event_outbox.dispatched.total once PER event_type, so
        // dispatched label cardinality tracks enqueued cardinality — the "partial coverage"
        // reported in #2923 is a low-volume observability artefact, not a code defect.
        const string aliasAlpha = "test.fake.event.alpha.2923";
        const string aliasBeta = "test.fake.event.beta.2923";

        var resolverMock = Mock.Get(_serviceProvider!.GetRequiredService<IDomainEventTypeResolver>());
        resolverMock.Setup(r => r.Resolve(aliasAlpha)).Returns(typeof(FakeEvent));
        resolverMock.Setup(r => r.Resolve(aliasBeta)).Returns(typeof(FakeEvent));

        var now = DateTimeOffset.UtcNow;
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var i = 0;
            foreach (var alias in new[] { aliasAlpha, aliasBeta })
            {
                var ev = new FakeEvent(Marker: i);
                var row = DomainEventOutboxEntity.Enqueue(
                    ev,
                    alias,
                    JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                    payloadVersion: 1,
                    correlationId: null,
                    now: now.AddSeconds(i));
                db.DomainEventOutbox.Add(row);
                i++;
            }
            await db.SaveChangesAsync(TestCancellationToken);
        }

        var dispatched = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (string.Equals(instrument.Meter.Name, MeepleAiMetrics.MeterName, StringComparison.Ordinal) &&
                    string.Equals(instrument.Name, "meepleai.domain_event_outbox.dispatched.total", StringComparison.Ordinal))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
            dispatched.Add((value, tags.ToArray())));
        listener.Start();

        // Act
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 100, cancellationToken: TestCancellationToken);

        // Assert
        processed.Should().Be(2);

        var dispatchedEventTypes = dispatched
            .Select(m => m.Tags.SingleOrDefault(t =>
                string.Equals(t.Key, "event_type", StringComparison.Ordinal)).Value as string)
            .ToList();

        dispatchedEventTypes.Should().Contain(aliasAlpha,
            "the dispatched counter must fire with event_type=alpha (#2923 cardinality parity)");
        dispatchedEventTypes.Should().Contain(aliasBeta,
            "the dispatched counter must fire with event_type=beta (#2923 cardinality parity)");
        dispatched
            .Where(m => m.Tags.Any(t =>
                string.Equals(t.Key, "event_type", StringComparison.Ordinal) &&
                (t.Value as string) is aliasAlpha or aliasBeta))
            .Should().AllSatisfy(m => m.Value.Should().Be(1,
                because: "each dispatched row emits exactly one counter tick"));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 2 — skip rows whose NextAttemptAt is in the future
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_SkipsRowsNotYetReady()
    {
        // Arrange: a Pending row scheduled 5 minutes in the future.
        var now = DateTimeOffset.UtcNow;
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var ev = new FakeEvent(Marker: 1);
            var row = DomainEventOutboxEntity.Enqueue(
                ev,
                FakeEventAlias,
                JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: now);
            // Simulate a retry-scheduled row: mark as retry to set NextAttemptAt forward.
            row.MarkRetry("forced", now.AddMinutes(5), now);
            db.DomainEventOutbox.Add(row);
            await db.SaveChangesAsync(TestCancellationToken);
        }

        // Act
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 100, cancellationToken: TestCancellationToken);

        // Assert: nothing dispatched, row stays Pending with original (post-MarkRetry) shape.
        processed.Should().Be(0,
            because: "the only Pending row has NextAttemptAt 5 minutes in the future");

        _mediatorMock!.Verify(
            m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no dispatch may happen for rows still in their backoff window");

        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.DomainEventOutbox.AsNoTracking().SingleAsync(TestCancellationToken);
            row.Status.Should().Be(DomainEventOutboxStatus.Pending);
            row.DispatchedAt.Should().BeNull();
            row.NextAttemptAt.Should().NotBeNull(
                because: "MarkRetry set the next-attempt window; the processor must not clear it");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 3 — FIFO ordering by EnqueuedAt when batch is undersized
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_RespectsFIFOByEnqueuedAt()
    {
        // Arrange: 5 Pending rows, staggered EnqueuedAt (t0, t0+1s … t0+4s), all ready.
        // We persist them in REVERSE insertion order so a naive query that relies on
        // table order would visibly fail the assertion.
        var now = DateTimeOffset.UtcNow;
        var enqueueTimes = Enumerable.Range(0, 5)
            .Select(i => now.AddSeconds(i))
            .ToArray();
        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            for (var i = enqueueTimes.Length - 1; i >= 0; i--)
            {
                var ev = new FakeEvent(Marker: i);
                var row = DomainEventOutboxEntity.Enqueue(
                    ev,
                    FakeEventAlias,
                    JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                    payloadVersion: 1,
                    correlationId: null,
                    now: enqueueTimes[i]);
                db.DomainEventOutbox.Add(row);
            }
            await db.SaveChangesAsync(TestCancellationToken);
        }

        // Act: drain only 3 of the 5 rows so we can observe which 3 are picked.
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 3, cancellationToken: TestCancellationToken);

        // Assert: the 3 OLDEST rows (by EnqueuedAt) transition to Sent; the 2 newest remain Pending.
        processed.Should().Be(3);

        await using (var verifyScope = _serviceProvider!.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var rows = await db.DomainEventOutbox.AsNoTracking()
                .OrderBy(r => r.EnqueuedAt)
                .ToListAsync(TestCancellationToken);
            rows.Should().HaveCount(5);

            rows.Take(3).Should().AllSatisfy(r =>
                r.Status.Should().Be(DomainEventOutboxStatus.Sent,
                    because: "the FIFO drain picks the oldest 3 rows first"));
            rows.Skip(3).Should().AllSatisfy(r =>
                r.Status.Should().Be(DomainEventOutboxStatus.Pending,
                    because: "the 2 newest rows must remain Pending after a batchSize-3 drain"));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // Test 4 — empty queue returns 0 AND refreshes the health snapshot
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_EmptyPending_ReturnsZero_AndUpdatesHealth()
    {
        // Arrange: nothing seeded — queue is empty.

        // Act
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 100, cancellationToken: TestCancellationToken);

        // Assert: zero rows considered, no dispatch, and the health snapshot was refreshed
        // with the "quiet system" values so observable gauges don't report stale counts.
        processed.Should().Be(0);

        _mediatorMock!.Verify(
            m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _healthMock!.Verify(
            t => t.RecordSnapshot(0, 0d, 0),
            Times.AtLeastOnce,
            "an empty batch must still refresh the snapshot to 0/0/0 — stale values would " +
            "mask a system that has caught up");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // T5 — Retry budget + dead-letter (failure path)
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_FailingPublish_MarksRetry_WithExponentialBackoff()
    {
        // Arrange: 1 Pending row ready immediately; mediator throws a transient error.
        // We override the mediator setup AFTER InitializeAsync wired the default no-op stub.
        const string transientMessage = "transient-T5-step1";
        _mediatorMock!
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(transientMessage));

        var fakeNow = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fakeNow);

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var ev = new FakeEvent(Marker: 1);
            var row = DomainEventOutboxEntity.Enqueue(
                ev,
                FakeEventAlias,
                JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: fakeNow);
            db.DomainEventOutbox.Add(row);
            await db.SaveChangesAsync(TestCancellationToken);
        }

        // Default options: MaxAttempts=10, InitialBackoffMs=1000, MaxBackoffSeconds=64.
        var processor = CreateProcessor(new DomainEventOutboxOptions(), timeProvider);

        // Act
        var processed = await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);

        // Assert: row REMAINS Pending (re-enters the queue), attempts incremented, LastError set,
        // NextAttemptAt scheduled to fakeNow + ~1s (±20% jitter).
        processed.Should().Be(1, "the row WAS considered in this batch (success/failure both count)");

        await using var verifyScope = _serviceProvider!.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var refreshed = await verifyDb.DomainEventOutbox.AsNoTracking().SingleAsync(TestCancellationToken);

        refreshed.LastError.Should().Be(transientMessage,
            because: "diagnostic: surface which catch branch the processor took");
        refreshed.Status.Should().Be(DomainEventOutboxStatus.Pending,
            because: "a transient failure with retry budget remaining must re-arm Pending, not Failed");
        refreshed.Attempts.Should().Be(1);
        refreshed.DispatchedAt.Should().BeNull();
        refreshed.NextAttemptAt.Should().NotBeNull();
        refreshed.NextAttemptAt!.Value.Should().BeOnOrAfter(fakeNow.AddMilliseconds(800),
            because: "backoff = InitialBackoffMs (1000) * 2^0 = 1s, jitter floor ≈ 0.8s");
        refreshed.NextAttemptAt!.Value.Should().BeOnOrBefore(fakeNow.AddMilliseconds(1200),
            because: "backoff = InitialBackoffMs (1000) * 2^0 = 1s, jitter ceiling ≈ 1.2s");
    }

    [Fact]
    public async Task RunOnceAsync_AfterMaxAttempts_MarksFailed_Terminal()
    {
        // Arrange: row has already failed twice (Attempts=2); the next failure exhausts a
        // MaxAttempts=3 budget and must transition to Failed (terminal).
        const string deterministicMessage = "deterministic-T5-step2";
        _mediatorMock!
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(deterministicMessage));

        var fakeNow = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fakeNow);

        await SeedRowWithAttemptsAsync(targetAttempts: 2, now: fakeNow);

        // Override MaxAttempts so the next failure tips the row into terminal Failed.
        var options = new DomainEventOutboxOptions { MaxAttempts = 3 };
        var processor = CreateProcessor(options, timeProvider);

        // Act
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);

        // Assert: Status=Failed, Attempts=3 (the entity's MarkFailed increments Attempts).
        await using var verifyScope = _serviceProvider!.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var refreshed = await verifyDb.DomainEventOutbox.AsNoTracking().SingleAsync(TestCancellationToken);

        refreshed.Status.Should().Be(DomainEventOutboxStatus.Failed,
            because: "Attempts(2)+1 == MaxAttempts(3) ⇒ the row must terminate, not re-schedule");
        refreshed.Attempts.Should().Be(3,
            because: "MarkFailed increments Attempts, so a row that had failed twice ends at 3");
        refreshed.LastError.Should().Be(deterministicMessage);
        refreshed.NextAttemptAt.Should().BeNull(
            because: "terminal Failed rows must not be re-attempted; NextAttemptAt is cleared");
    }

    [Fact]
    public async Task RunOnceAsync_Backoff_CapsAtMaxBackoffSeconds()
    {
        // Arrange: simulate a row that has failed 10 times. The raw exponential backoff
        // for attempt 11 would be 1024s (≫ cap). MaxBackoffSeconds=8 is a STRICT ceiling —
        // jitter is applied to the un-capped value FIRST, then the cap clamps. So the final
        // window is [0.8s × min(jittered, 8s), min(jittered, 8s)] but capped at 8s strictly:
        // floor = lowest possible jittered value above 0 (theoretically near 0 if jitter
        // multiplied by an already-large unbounded value is shrunk below 8); practically the
        // unbounded × 0.8 dominates so the jittered value is always > 8 → cap kicks in → exactly 8s.
        // For attempt 11 with InitialBackoffMs=1000: unbounded = 1024s, jittered ∈ [819.2s, 1228.8s],
        // both ends > 8s → result is ALWAYS exactly 8s (no jitter visible).
        const string transientMessage = "transient-T5-step3-cap";
        _mediatorMock!
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(transientMessage));

        var fakeNow = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fakeNow);

        await SeedRowWithAttemptsAsync(targetAttempts: 10, now: fakeNow);

        // Crank MaxAttempts high so this failure stays in retry territory (we are testing
        // the BACKOFF math, not the dead-letter transition).
        var options = new DomainEventOutboxOptions { MaxAttempts = 20, MaxBackoffSeconds = 8.0 };
        var processor = CreateProcessor(options, timeProvider);

        // Act
        await processor.RunOnceAsync(batchSize: 10, cancellationToken: TestCancellationToken);

        // Assert: row Pending, Attempts=11, NextAttemptAt-now ∈ [6.4s, 9.6s].
        await using var verifyScope = _serviceProvider!.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var refreshed = await verifyDb.DomainEventOutbox.AsNoTracking().SingleAsync(TestCancellationToken);

        refreshed.Status.Should().Be(DomainEventOutboxStatus.Pending);
        refreshed.Attempts.Should().Be(11);
        refreshed.NextAttemptAt.Should().NotBeNull();

        var delay = (refreshed.NextAttemptAt!.Value - fakeNow).TotalSeconds;
        delay.Should().BeLessThanOrEqualTo(8.0,
            because: "MaxBackoffSeconds=8 is a STRICT ceiling — cap is applied AFTER jitter");
        delay.Should().BeGreaterThan(0,
            because: "the row must be re-armed in the future, not immediately");
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
    /// Builds a <see cref="DomainEventOutboxProcessor"/> with per-test option overrides and
    /// an optional <see cref="TimeProvider"/>. T5 tests need to deterministically assert on
    /// backoff windows, so the test rig replaces the live <c>TimeProvider.System</c> with a
    /// <c>FakeTimeProvider</c> pinned to a known instant.
    /// </summary>
    private DomainEventOutboxProcessor CreateProcessor(
        DomainEventOutboxOptions options,
        TimeProvider? timeProvider = null)
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
    /// Seeds a single Pending row and then drives <see cref="DomainEventOutboxEntity.MarkRetry"/>
    /// <paramref name="targetAttempts"/> times to simulate prior failure history. After each
    /// MarkRetry the <c>NextAttemptAt</c> is anchored 1 second IN THE PAST so the processor
    /// still treats the row as ready when it polls at <paramref name="now"/>.
    /// </summary>
    private async Task SeedRowWithAttemptsAsync(int targetAttempts, DateTimeOffset now)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var ev = new FakeEvent(Marker: 1);
        var row = DomainEventOutboxEntity.Enqueue(
            ev,
            FakeEventAlias,
            JsonSerializer.Serialize(ev, DomainEventJsonOptions.Default),
            payloadVersion: 1,
            correlationId: null,
            now: now);
        for (var i = 0; i < targetAttempts; i++)
        {
            row.MarkRetry($"seed-history-{i}", now.AddSeconds(-1), now);
        }
        db.DomainEventOutbox.Add(row);
        await db.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Minimal <see cref="IDomainEvent"/> test fixture. Internal to Api.Tests — the
    /// production <see cref="DomainEventTypeResolver"/> scans the Api assembly and
    /// therefore will not see this type; we register an explicit mapping on the
    /// resolver mock in <see cref="InitializeAsync"/>.
    /// </summary>
    internal sealed record FakeEvent : IDomainEvent
    {
        public FakeEvent(int Marker)
        {
            this.Marker = Marker;
            EventId = Guid.NewGuid();
            OccurredAt = DateTime.UtcNow;
        }

        // Parameterless constructor for System.Text.Json round-trip via DomainEventJsonOptions.
        public FakeEvent() : this(0) { }

        public int Marker { get; init; }
        public Guid EventId { get; init; }
        public DateTime OccurredAt { get; init; }
    }
}

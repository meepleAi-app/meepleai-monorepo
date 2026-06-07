using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
